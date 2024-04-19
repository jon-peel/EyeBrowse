using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.ReactiveUI;
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

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (DataContext is not MainWindowViewModel vm) return;

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
}