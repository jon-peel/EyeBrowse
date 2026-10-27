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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) Close();
    }
}
