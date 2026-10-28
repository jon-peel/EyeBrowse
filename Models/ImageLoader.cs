using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using BitMiracle.LibTiff.Classic;
using SkiaSharp;

namespace EyeBrowse.Models;

/// <summary>A decoded image: one frame, or the frames of an animation and how long each shows.</summary>
public record LoadedImage(Bitmap[] Frames, TimeSpan[] Durations)
{
    public bool IsAnimated => Frames.Length > 1;
}

public static class ImageLoader
{
    // Browsers show GIF/WebP frames with a delay of 10 ms or less (usually 0, meaning
    // "unset") for 100 ms, and so does EyeBrowse.
    static readonly TimeSpan MaxUnsetFrameDuration = TimeSpan.FromMilliseconds(10);
    static readonly TimeSpan DefaultFrameDuration = TimeSpan.FromMilliseconds(100);

    static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp"];

    /// <summary>Whether a file or archive entry name has a supported image extension.</summary>
    public static bool IsImage(string name)
    {
        // macOS writes "._name" metadata files (AppleDouble) next to files on non-Mac
        // drives and into zips; they share the image's extension but aren't images.
        return Extensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase)
               && !Path.GetFileName(name).StartsWith("._", StringComparison.Ordinal);
    }

    public static LoadedImage Load(string path)
    {
        return Load(path, File.ReadAllBytes(path));
    }

    /// <summary>Decodes image data; <paramref name="name"/> is only used for its extension.</summary>
    public static LoadedImage Load(string name, byte[] data)
    {
        return Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".tif" or ".tiff" => new([LoadTiff(data)], [TimeSpan.Zero]),
            _ => LoadWithSkia(data)
        };
    }

    // Decodes with Skia directly rather than through Avalonia's Bitmap(path), which
    // ignores EXIF orientation and only ever decodes the first frame.
    static LoadedImage LoadWithSkia(byte[] data)
    {
        // Skia reads the array in place (pinned until Skia releases it) rather than a copy.
        var pin = GCHandle.Alloc(data, GCHandleType.Pinned);
        using var skData = SKData.Create(pin.AddrOfPinnedObject(), data.Length, (_, _) => pin.Free());
        using var codec = SKCodec.Create(skData) ?? throw new InvalidDataException("not a readable image");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var frameInfos = codec.FrameCount > 1 ? codec.FrameInfo : [];

        if (frameInfos.Length == 0)
        {
            using var single = Decode(codec, info, new SKCodecOptions(0), null);
            return new([ToAvalonia(Orient(single, codec.EncodedOrigin))], [TimeSpan.Zero]);
        }

        // Frames are stored as changes to an earlier frame, so keep every decoded frame
        // in Skia form until the animation is complete.
        var decoded = new SKBitmap[frameInfos.Length];
        try
        {
            for (var i = 0; i < frameInfos.Length; i++)
            {
                var required = frameInfos[i].RequiredFrame;
                decoded[i] = Decode(codec, info, new SKCodecOptions(i, required), required >= 0 ? decoded[required] : null);
            }

            var frames = decoded.Select(frame => ToAvalonia(Orient(frame, codec.EncodedOrigin))).ToArray();
            var durations = frameInfos
                .Select(f => f.Duration <= MaxUnsetFrameDuration.TotalMilliseconds ? DefaultFrameDuration : TimeSpan.FromMilliseconds(f.Duration))
                .ToArray();
            return new(frames, durations);
        }
        finally
        {
            foreach (var frame in decoded) frame?.Dispose();
        }
    }

    static SKBitmap Decode(SKCodec codec, SKImageInfo info, SKCodecOptions options, SKBitmap? priorFrame)
    {
        var bitmap = new SKBitmap(info);
        priorFrame?.CopyTo(bitmap);
        var result = codec.GetPixels(info, bitmap.GetPixels(), options);
        // A truncated file still yields whatever was decoded before the data ran out.
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bitmap.Dispose();
            throw new InvalidDataException($"cannot decode image: {result}");
        }

        return bitmap;
    }

    // Returns a bitmap drawn the right way up; disposes the source if it had to redraw it.
    static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft) return source;

        var (w, h) = (source.Width, source.Height);
        var swapsAxes = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var oriented = new SKBitmap(new SKImageInfo(swapsAxes ? h : w, swapsAxes ? w : h, source.ColorType, source.AlphaType));
        using (var canvas = new SKCanvas(oriented))
        {
            canvas.SetMatrix(origin switch
            {
                SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
                SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
                SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
                SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
                SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
                SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
                SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
                _ => SKMatrix.Identity
            });
            canvas.DrawBitmap(source, 0, 0);
        }

        source.Dispose();
        return oriented;
    }

    // Copies the pixels into an Avalonia bitmap and disposes the Skia one.
    static Bitmap ToAvalonia(SKBitmap source)
    {
        using (source)
            return new(PixelFormat.Rgba8888, AlphaFormat.Premul, source.GetPixels(),
                new(source.Width, source.Height), new(96, 96), source.RowBytes);
    }

    // Skia, which Avalonia decodes images with, has no TIFF support.
    static Bitmap LoadTiff(byte[] data)
    {
        using var tiff = Tiff.ClientOpen("image", "r", new MemoryStream(data, writable: false), new TiffStream())
                         ?? throw new InvalidDataException("not a readable TIFF");
        var width = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
        var height = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
        return LoadGreyAlphaTiff(tiff, width, height) ?? LoadRgbaTiff(tiff, width, height);
    }

    static Bitmap LoadRgbaTiff(Tiff tiff, int width, int height)
    {
        // Each int is packed R | G << 8 | B << 16 | A << 24, i.e. RGBA bytes in memory,
        // with the colour already multiplied by alpha whatever the file stores.
        var raster = new int[width * height];
        if (!tiff.ReadRGBAImageOriented(width, height, raster, Orientation.TOPLEFT))
            throw new InvalidDataException("cannot decode this TIFF");
        return ToAvalonia(raster, width, height, AlphaFormat.Premul);
    }

    // The RGBA reader above drops the alpha of grey + alpha images, so read 8-bit ones
    // (the usual kind) directly. Returns null for anything else.
    static Bitmap? LoadGreyAlphaTiff(Tiff tiff, int width, int height)
    {
        int Field(TiffTag tag, int fallback) => tiff.GetField(tag)?[0].ToInt() ?? fallback;
        if (Field(TiffTag.PHOTOMETRIC, -1) != (int)Photometric.MINISBLACK || Field(TiffTag.SAMPLESPERPIXEL, 1) != 2
            || Field(TiffTag.BITSPERSAMPLE, 1) != 8 || Field(TiffTag.PLANARCONFIG, 1) != (int)PlanarConfig.CONTIG
            || tiff.IsTiled())
            return null;

        var extraSamples = tiff.GetField(TiffTag.EXTRASAMPLES);
        var associated = extraSamples is not null && extraSamples[1].ToShortArray()[0] == (short)ExtraSample.ASSOCALPHA;
        var rgba = new byte[width * height * 4];
        var line = new byte[tiff.ScanlineSize()];
        for (var y = 0; y < height; y++)
        {
            if (!tiff.ReadScanline(line, y))
                throw new InvalidDataException("cannot decode this TIFF");
            for (var x = 0; x < width; x++)
            {
                var (grey, alpha, i) = (line[2 * x], line[2 * x + 1], (y * width + x) * 4);
                (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]) = (grey, grey, grey, alpha);
            }
        }

        return ToAvalonia(rgba, width, height, associated ? AlphaFormat.Premul : AlphaFormat.Unpremul);
    }

    // Copies RGBA pixels held in a managed array into an Avalonia bitmap.
    static Bitmap ToAvalonia(Array pixels, int width, int height, AlphaFormat alphaFormat)
    {
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            return new(PixelFormat.Rgba8888, alphaFormat, handle.AddrOfPinnedObject(),
                new(width, height), new(96, 96), width * 4);
        }
        finally
        {
            handle.Free();
        }
    }
}
