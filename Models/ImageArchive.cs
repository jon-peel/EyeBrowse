using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace EyeBrowse.Models;

/// <summary>Zip archives of images, including comic book (.cbz) archives.</summary>
public static class ImageArchive
{
    static readonly string[] Extensions = [".zip", ".cbz"];

    public static bool IsArchive(string path)
    {
        return Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Names of the image entries, in the order given by <paramref name="order"/>.</summary>
    public static string[] ListImages(string archivePath, StringComparer order)
    {
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

    // Zips made on macOS carry a __MACOSX folder of "._name" metadata files that
    // share the images' extensions but aren't images.
    static bool IsMacJunk(string fullName)
    {
        return fullName.StartsWith("__MACOSX/", StringComparison.Ordinal)
               || Path.GetFileName(fullName).StartsWith("._", StringComparison.Ordinal);
    }
}
