using System.Threading.Tasks;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using Avalonia.Controls;
using Avalonia.Input;
using ReactiveUI.Avalonia;
using EyeBrowse.ViewModels;
using ReactiveUI;
using Direction = EyeBrowse.ViewModels.MainWindowViewModel.Direction;
using Browse = EyeBrowse.ViewModels.MainWindowViewModel.Browse;
using Zoom = EyeBrowse.ViewModels.MainWindowViewModel.Zoom;

namespace EyeBrowse.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
        Cursor = new(StandardCursorType.None);
#if !DEBUG
        WindowState = WindowState.FullScreen;
#endif
        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, viewModel => viewModel.PictureSize, view => view.Picture.Width, s => s.Width)
                .DisposeWith(disposables);
            this.OneWayBind(ViewModel, viewModel => viewModel.PictureSize, view => view.Picture.Height, s => s.Height)
                .DisposeWith(disposables);
        });
    }

    protected override async void OnResized(WindowResizedEventArgs e)
    {
        base.OnResized(e);
        if (DataContext is not MainWindowViewModel vm) return;
        await vm.ResizeCommand.Execute(e.ClientSize);
    }

    // EyeBrowse is keyboard-only; a click explains how to use it.
    protected override async void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        await ShowHelp();
    }

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (DataContext is not MainWindowViewModel vm) return;

        // The arrow keys do nothing in EyeBrowse, so they show the help, as H, F1 and ? do.
        // "?" is matched by the character typed, as its key differs between keyboard layouts.
        if (e.Key is Key.H or Key.F1 or Key.Left or Key.Right or Key.Up or Key.Down || e.KeySymbol == "?")
        {
            await ShowHelp();
            return;
        }

        var action = e.Key switch
        {
            Key.Q => (Browse.Previous, null, null),
            Key.E => (Browse.Next, null, null),
            Key.W => (null, Direction.Up, null),
            Key.S => (null, Direction.Down, null),
            Key.A => (null, Direction.Left, null),
            Key.D => (null, Direction.Right, null),
            Key.C => (null, null, Zoom.True),
            Key.X => (null, null, Zoom.Fit),
            Key.Z => (null, null, Zoom.Fill),
            Key.Escape => CloseNow(),
            _ => (null, null, null)
        };

        if (action is ({ } browse, _, _)) await vm.BrowseCommand.Execute(browse);
        if (action is (_, { } direction, _)) await vm.PanCommand.Execute(direction);
        if (action is (_, _, { } zoom)) await vm.ZoomCommand.Execute(zoom);
        return;

        (Browse?, Direction?, Zoom?) CloseNow()
        {
            Close();
            return (null, null, null);
        }
    }

    // Modal, so keys go to the help window (Esc closes it) until it's closed.
    Task ShowHelp()
    {
        return new HelpWindow { WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(this);
    }
}