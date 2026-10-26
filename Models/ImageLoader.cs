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
    // GIF/WebP frames with no (or a silly) delay are shown for this long, as browsers do.
    static readonly TimeSpan MinFrameDuration = TimeSpan.FromMilliseconds(20);
    static readonly TimeSpan DefaultFrameDuration = TimeSpan.FromMilliseconds(100);

    public static LoadedImage Load(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".tif" or ".tiff" => new([LoadTiff(path)], [TimeSpan.Zero]),
            _ => LoadWithSkia(path)
        };
    }

    // Decodes with Skia directly rather than through Avalonia's Bitmap(path), which
    // ignores EXIF orientation and only ever decodes the first frame.
    static LoadedImage LoadWithSkia(string path)
    {
        using var codec = SKCodec.Create(path) ?? throw new InvalidDataException($"cannot read image {path}");
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
                .Select(f => f.Duration < MinFrameDuration.TotalMilliseconds ? DefaultFrameDuration : TimeSpan.FromMilliseconds(f.Duration))
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
    static Bitmap LoadTiff(string path)
    {
        using var tiff = Tiff.Open(path, "r") ?? throw new InvalidDataException($"cannot read TIFF {path}");
        var width = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
        var height = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();

        // Each int is packed R | G << 8 | B << 16 | A << 24, i.e. RGBA bytes in memory.
        var raster = new int[width * height];
        if (!tiff.ReadRGBAImageOriented(width, height, raster, Orientation.TOPLEFT))
            throw new InvalidDataException($"cannot decode TIFF {path}");

        var handle = GCHandle.Alloc(raster, GCHandleType.Pinned);
        try
        {
            return new(PixelFormat.Rgba8888, AlphaFormat.Unpremul, handle.AddrOfPinnedObject(),
                new(width, height), new(96, 96), width * 4);
        }
        finally
        {
            handle.Free();
        }
    }
}
