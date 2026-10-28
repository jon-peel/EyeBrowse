using System;
using Avalonia.Controls;
using Avalonia.Input;
using EyeBrowse.Models;

namespace EyeBrowse.Views;

/// <summary>Usage, keys and settings; also shown when EyeBrowse has no image to open.</summary>
public partial class HelpWindow : Window
{
    readonly Settings _settings = Settings.Current;
    // Each setting's row, its tick, and how to read and change it.
    readonly (Border row, Control check, Func<bool> get, Action<bool> set)[] _rows;
    int _selected;

    public HelpWindow() : this(null)
    {
    }

    public HelpWindow(string? error)
    {
        InitializeComponent();
        ErrorPanel.IsVisible = error is not null;
        ErrorText.Text = error;
        _rows = [(KeepPanRow, KeepPanCheck, () => _settings.KeepPan, on => _settings.KeepPan = on)];
        for (var i = 0; i < _rows.Length; i++)
        {
            var row = i;
            _rows[i].row.PointerPressed += (_, e) =>
            {
                _selected = row;
                Toggle();
                ShowSettings();
                e.Handled = true;
            };
        }
        ShowSettings();
    }

    void Toggle()
    {
        var (_, _, get, set) = _rows[_selected];
        set(!get());
        _settings.Save();
    }

    void ShowSettings()
    {
        for (var i = 0; i < _rows.Length; i++)
        {
            _rows[i].row.Classes.Set("selected", i == _selected);
            _rows[i].check.IsVisible = _rows[i].get();
        }
    }

    // Over the full-screen image window the help doesn't always get keyboard focus by
    // itself, and Esc must reach it.
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Activate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Escape: Close(); break;
            // A only here: in the image window it pans.
            case Key.A: new AboutWindow().ShowDialog(this); break;
            case Key.Up: _selected = Math.Max(_selected - 1, 0); break;
            case Key.Down: _selected = Math.Min(_selected + 1, _rows.Length - 1); break;
            case Key.Space: Toggle(); break;
            default: return;
        }
        e.Handled = true;
        ShowSettings();
    }
}
