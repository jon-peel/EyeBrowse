using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EyeBrowse.ViewModels;
using EyeBrowse.Views;

namespace EyeBrowse;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = CreateMainWindow(desktop.Args?.FirstOrDefault());

        base.OnFrameworkInitializationCompleted();
    }

    // With nothing to open (e.g. the executable was double-clicked) or something that
    // can't be opened, explain how EyeBrowse is used instead of exiting silently.
    static Window CreateMainWindow(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return new HelpWindow();

        try
        {
            return new MainWindow { DataContext = new MainWindowViewModel(path) };
        }
        catch (Exception e) when (e is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return new HelpWindow($"Couldn't open {path}: {e.Message}");
        }
    }
}