using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using EyeBrowse.ViewModels;
using EyeBrowse.Views;

namespace EyeBrowse;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // Set while an error is on screen, so one that repeats doesn't pile up dialogs.
    static bool _reportingError;

    public override void OnFrameworkInitializationCompleted()
    {
        // A last resort: an unexpected error on the UI thread (including from async void
        // handlers) is reported and EyeBrowse carries on, instead of vanishing.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            e.Handled = true;
            ReportError(e.Exception);
        };

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

    /// <summary>Shows an unexpected error over the main window. Nothing is written to disk.</summary>
    public static void ReportError(Exception error) => Dispatcher.UIThread.Post(async () =>
    {
        if (_reportingError || Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { IsVisible: true } owner })
            return;

        _reportingError = true;
        try
        {
            await new HelpWindow($"Something went wrong: {error.Message}") { WindowStartupLocation = WindowStartupLocation.CenterOwner }
                .ShowDialog(owner);
        }
        catch
        {
            // Reporting failed too; it mustn't come back here as another error.
        }
        finally
        {
            _reportingError = false;
        }
    });
}
