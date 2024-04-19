using System;
using System.Linq;
using Avalonia;
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
            desktop.MainWindow = desktop.Args?.FirstOrDefault() switch
            {
                null => throw new ArgumentException("no image path provided"),
                { } imagePath => new MainWindow { DataContext = new MainWindowViewModel(imagePath) }
            };

        base.OnFrameworkInitializationCompleted();
    }
}