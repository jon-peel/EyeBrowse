using System;
using Avalonia;
using ReactiveUI.Avalonia;

namespace EyeBrowse;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI(rx => rx.WithExceptionHandler(new ErrorReporter()));
    }

    // An error that breaks a ReactiveUI pipeline (a command or derived property). ReactiveUI's
    // default rethrows it on the UI thread wrapped in a generic message; this reports the cause.
    sealed class ErrorReporter : IObserver<Exception>
    {
        public void OnNext(Exception value) => App.ReportError(value);
        public void OnError(Exception error) => App.ReportError(error);
        public void OnCompleted() { }
    }
}