using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zoom = EyeBrowse.ViewModels.MainWindowViewModel.Zoom;

namespace EyeBrowse.Models;

/// <summary>
/// Preferences, saved in the user's profile. EyeBrowse keeps no history: nothing here
/// records which files or folders were opened, or where the reader was in them.
/// </summary>
public sealed class Settings
{
    // %APPDATA%\EyeBrowse on Windows (redirected into the package when installed from
    // MSIX), ~/.config/EyeBrowse elsewhere.
    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EyeBrowse", "settings.txt");

    public static Settings Current { get; } = Load();

    // The one choice made in the help window.
    public bool KeepPan { get; set; } = true;

    // The modes as last left, restored next time. One or two pages is kept apart for comic
    // archives and folders, as photos rarely want two pages.
    public Zoom Zoom { get; set; } = Zoom.True;
    public bool ComicSpread { get; set; }
    public bool FolderSpread { get; set; }
    public bool ShowOverlay { get; set; } = true;

    // Plain "name=value" lines: small, readable, and nothing for trimming to break.
    static Settings Load()
    {
        var settings = new Settings();
        Dictionary<string, string> values;
        try
        {
            values = File.ReadAllLines(FilePath)
                .Select(line => line.Split('=', 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return settings; // first run, or unreadable: the defaults
        }

        settings.KeepPan = Bool("KeepPan", settings.KeepPan);
        settings.ComicSpread = Bool("ComicSpread", settings.ComicSpread);
        settings.FolderSpread = Bool("FolderSpread", settings.FolderSpread);
        settings.ShowOverlay = Bool("ShowOverlay", settings.ShowOverlay);
        if (values.TryGetValue("Zoom", out var zoom) && Enum.TryParse<Zoom>(zoom, out var parsed) && Enum.IsDefined(parsed))
            settings.Zoom = parsed;
        return settings;

        bool Bool(string name, bool fallback) =>
            values.TryGetValue(name, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    /// <summary>Writes the settings; a profile that can't be written just doesn't remember them.</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllLines(FilePath,
            [
                $"KeepPan={KeepPan}",
                $"Zoom={Zoom}",
                $"ComicSpread={ComicSpread}",
                $"FolderSpread={FolderSpread}",
                $"ShowOverlay={ShowOverlay}",
            ]);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
