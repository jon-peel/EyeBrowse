using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using BitMiracle.LibTiff.Classic;

namespace EyeBrowse.Models;

public static class ImageLoader
{
    public static Bitmap Load(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".tif" or ".tiff" => LoadTiff(path),
            _ => new(path)
        };
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
