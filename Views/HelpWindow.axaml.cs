using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace EyeBrowse.Views;

/// <summary>Usage and keys, shown when EyeBrowse has no image to open.</summary>
public partial class HelpWindow : Window
{
    public HelpWindow() : this(null)
    {
    }

    public HelpWindow(string? error)
    {
        InitializeComponent();
        ErrorPanel.IsVisible = error is not null;
        ErrorText.Text = error;
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
        if (e.Key == Key.Escape) Close();
        // A only here: in the image window it pans.
        else if (e.Key == Key.A) new AboutWindow().ShowDialog(this);
    }
}
