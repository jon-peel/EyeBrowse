using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace EyeBrowse.Models;

/// <summary>Zip archives of images, including comic book (.cbz) archives.</summary>
public static class ImageArchive
{
    // Only zip is read, but comic archives are often misnamed, e.g. a .cbr that is really
    // a zip, so every comic book extension is accepted and the content decides.
    static readonly string[] Extensions = [".zip", ".cbz", ".cbr", ".cb7", ".cbt"];

    public static bool IsArchive(string path)
    {
        return Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Names of the image entries, in the order given by <paramref name="order"/>.</summary>
    public static string[] ListImages(string archivePath, StringComparer order)
    {
        var format = Identify(archivePath);
        if (format != "zip")
            throw new ArgumentException(format is null
                ? "not a zip archive"
                : $"it's a {format} archive, and EyeBrowse only opens zip-based ones (.cbz, .zip)");

        using var zip = ZipFile.OpenRead(archivePath);
        return zip.Entries
            .Where(entry => entry.Name.Length > 0 && ImageLoader.IsImage(entry.Name) && !IsMacJunk(entry.FullName))
            .Select(entry => entry.FullName)
            .OrderBy(name => name, order)
            .ToArray();
    }

    public static LoadedImage Load(string archivePath, string entryName)
    {
        using var zip = ZipFile.OpenRead(archivePath);
        var entry = zip.GetEntry(entryName) ?? throw new FileNotFoundException($"{entryName} is no longer in the archive");
        using var stream = entry.Open();
        using var data = new MemoryStream();
        stream.CopyTo(data);
        return ImageLoader.Load(entryName, data.ToArray());
    }

    // What the file really is, from its first bytes; null if not a known archive format.
    static string? Identify(string path)
    {
        var head = new byte[262];
        using (var file = File.OpenRead(path))
            file.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);

        bool StartsWith(params byte[] magic) => head.AsSpan().StartsWith(magic);
        if (StartsWith(0x50, 0x4B, 0x03, 0x04) || StartsWith(0x50, 0x4B, 0x05, 0x06)) return "zip";
        if (StartsWith(0x52, 0x61, 0x72, 0x21, 0x1A, 0x07)) return "RAR";
        if (StartsWith(0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C)) return "7-Zip";
        if (head.AsSpan(257, 5).SequenceEqual("ustar"u8)) return "tar";
        return null;
    }

    // Zips made on macOS carry a __MACOSX folder of "._name" metadata files that
    // share the images' extensions but aren't images.
    static bool IsMacJunk(string fullName)
    {
        return fullName.StartsWith("__MACOSX/", StringComparison.Ordinal)
               || Path.GetFileName(fullName).StartsWith("._", StringComparison.Ordinal);
    }
}
