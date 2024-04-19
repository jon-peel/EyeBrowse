using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Media.Imaging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace EyeBrowse.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public enum Browse
    {
        Previous,
        Next
    }

    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    public enum Zoom
    {
        True,
        Fit,
        Fill
    }

    readonly (string path, Lazy<Bitmap> picture)[] _dirImages;
    readonly ObservableAsPropertyHelper<Bitmap> _picture;
    readonly ObservableAsPropertyHelper<Size> _pictureSize;
    readonly ObservableAsPropertyHelper<Point> _position;
    readonly ObservableAsPropertyHelper<string> _title;
    readonly ObservableAsPropertyHelper<Size> _windowSize;


    public MainWindowViewModel(string initialPath)
    {
        Offset = (0, 0);
        (Index, _dirImages) = GetDirImageIndex(initialPath);

        Display = Zoom.True;
        BrowseCommand = ReactiveCommand.Create<Browse>(BrowseImage);
        PanCommand = ReactiveCommand.Create<Direction>(PanImage);
        ZoomCommand = ReactiveCommand.Create<Zoom>(ZoomImage);
        ResizeCommand = ReactiveCommand.Create<Size, Size>(x => x);

        _windowSize = ResizeCommand
            .Throttle(TimeSpan.FromMilliseconds(100))
            .ToProperty(this, x => x.WindowSize);

        _title = this
            .WhenAnyValue(x => x.Index)
            .Select(x => $"[{x + 1}/{_dirImages.Length}] {_dirImages[x].path}")
            .ToProperty(this, x => x.Title);

        _picture = this
            .WhenAnyValue(x => x.Index)
            .Select(x => _dirImages[x].picture.Value)
            .ToProperty(this, x => x.Picture);

        _pictureSize = this
            .WhenAnyValue(x => x.Picture, x => x.WindowSize, x => x.Display)
            .Select(_ => SizePicture())
            .ToProperty(this, x => x.PictureSize);

        _position = this
            .WhenAnyValue(x => x.PictureSize, x => x.Offset, x => x.WindowSize)
            .Select(x => PositionPicture())
            .ToProperty(this, x => x.PicturePosition);
    }

    [Reactive] int Index { get; set; }
    [Reactive] (int x, int y) Offset { get; set; }
    [Reactive] Zoom Display { get; set; }

    public string Title => _title.Value;
    public Bitmap Picture => _picture.Value;
    public Size WindowSize => _windowSize.Value;
    public Size PictureSize => _pictureSize.Value;
    public Point PicturePosition => _position.Value;
    public ReactiveCommand<Browse, Unit> BrowseCommand { get; }
    public ReactiveCommand<Direction, Unit> PanCommand { get; }
    public ReactiveCommand<Zoom, Unit> ZoomCommand { get; }
    public ReactiveCommand<Size, Size> ResizeCommand { get; }

    Point PositionPicture()
    {
        var top = GetPoint(PictureSize.Height, WindowSize.Height, Offset.y);
        var left = GetPoint(PictureSize.Width, WindowSize.Width, Offset.x);

        return new(left, top);

        static double GetPoint(double pic, double win, int offset)
        {
            if (pic <= win || win <= 0) return (win - pic) / 2.0;

            var d = (pic - win) / 2.0;
            var p = d / 100.0 * offset;
            return p - d;
        }
    }


    Size SizePicture()
    {
        var ratio = Picture.Size.AspectRatio;
        return Display switch
        {
            Zoom.True => Picture.Size,
            Zoom.Fill =>
                WindowSize.Height * ratio <= WindowSize.Width
                    ? new(WindowSize.Width, WindowSize.Width / ratio)
                    : new(WindowSize.Height * ratio, WindowSize.Height),
            Zoom.Fit =>
                WindowSize.Height * ratio <= WindowSize.Width
                    ? new(WindowSize.Height * ratio, WindowSize.Height)
                    : new(WindowSize.Width, WindowSize.Width / ratio),
            _ => PictureSize
        };
    }


    void BrowseImage(Browse browse)
    {
        Index = browse switch
        {
            Browse.Previous when Index > 0 => Index - 1,
            Browse.Next when Index < _dirImages.Length - 1 => Index + 1,
            _ => Index
        };
    }

    void ZoomImage(Zoom zoom)
    {
        Display = zoom;
    }

    void PanImage(Direction direction)
    {
        Offset = direction switch
        {
            Direction.Up when Offset.y < 100 => (Offset.x, Offset.y + 10),
            Direction.Down when Offset.y > -100 => (Offset.x, Offset.y - 10),
            Direction.Left when Offset.x > -100 => (Offset.x - 10, Offset.y),
            Direction.Right when Offset.x < 100 => (Offset.x + 10, Offset.y),
            _ => Offset
        };
    }

    static (int index, (string, Lazy<Bitmap>)[] dirImages) GetDirImageIndex(string initialPath)
    {
        string[] ext = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", "webp"];
        var directory = Path.GetDirectoryName(initialPath)!;
        var imagePaths = Directory
            .GetFiles(directory)
            .Where(file => ext.Any(x => Path.GetExtension(file).Equals(x, StringComparison.InvariantCultureIgnoreCase)))
            .ToArray();
        var index = Array.IndexOf(imagePaths, initialPath);
        var images = imagePaths
            .Select(path => (path, new Lazy<Bitmap>(() => new(path))))
            .ToArray();
        return (index, images);
    }
}