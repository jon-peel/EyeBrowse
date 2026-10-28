using System;
using System.IO;
using System.Reflection;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;

namespace EyeBrowse.Views;

/// <summary>Version, licence and third-party notices, opened from the help window.</summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = "Version " + Version();
        using var reader = new StreamReader(AssetLoader.Open(new Uri("avares://EyeBrowse/Assets/ThirdPartyNotices.txt")));
        NoticesList.ItemsSource = Unwrap(reader.ReadToEnd())
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    // Most notices are wrapped at 72-80 columns, wider than the window, so rejoin those
    // lines to wrap at the window's width. Short lines, rules, indented lines, list items
    // and comment lines keep their breaks. (Not a Regex: that adds about 100 KB.)
    static string Unwrap(string text)
    {
        var lines = text.Split('\n');
        var result = new StringBuilder(text.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            result.Append(line);
            if (i == lines.Length - 1) break;
            var next = lines[i + 1];
            var joins = line.Length >= 60 && line[^1] is not (' ' or '=' or '-')
                        && next.Length > 0 && !char.IsWhiteSpace(next[0]) && !char.IsDigit(next[0])
                        && next[0] is not ('*' or '-' or '/' or '#');
            result.Append(joins ? ' ' : '\n');
        }
        return result.ToString();
    }

    // CI stamps the version from the git tag; the SDK appends "+<commit>", which isn't for people.
    static string Version()
    {
        var version = typeof(AboutWindow).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        var plus = version.IndexOf('+');
        return plus < 0 ? version : version[..plus];
    }

    // Opened over the help window, which may itself be a dialog over the full-screen
    // image, so make sure the keys come here.
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Activate();
    }

    // Nothing here takes focus, so the window scrolls the notices itself.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Escape: Close(); break;
            case Key.Up: Notices.LineUp(); break;
            case Key.Down: Notices.LineDown(); break;
            case Key.PageUp: Notices.PageUp(); break;
            case Key.PageDown: Notices.PageDown(); break;
            case Key.Home: Notices.ScrollToHome(); break;
            case Key.End: Notices.ScrollToEnd(); break;
            default: return;
        }
        e.Handled = true;
    }
}
