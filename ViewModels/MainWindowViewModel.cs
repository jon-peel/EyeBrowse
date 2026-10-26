using System;
using System.Globalization;
using System.IO;
using System.Linq;
using ReactiveUI.Primitives;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using EyeBrowse.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace EyeBrowse.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
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

    readonly DispatcherTimer _animation = new();
    readonly (string path, Lazy<LoadedImage> image)[] _dirImages;
    readonly ObservableAsPropertyHelper<bool> _isFill;
    readonly ObservableAsPropertyHelper<bool> _isFit;
    readonly ObservableAsPropertyHelper<bool> _isTrueSize;
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
            .WhenAnyValue(x => x.Index, x => x.Frame, (_, frame) => CurrentImage.Frames[frame % CurrentImage.Frames.Length])
            .ToProperty(this, x => x.Picture);

        _animation.Tick += (_, _) => NextFrame();
        this.WhenAnyValue(x => x.Index).Subscribe(_ => StartAnimation());

        _isFill = this.WhenAnyValue(x => x.Display).Select(x => x == Zoom.Fill).ToProperty(this, x => x.IsFill);
        _isFit = this.WhenAnyValue(x => x.Display).Select(x => x == Zoom.Fit).ToProperty(this, x => x.IsFit);
        _isTrueSize = this.WhenAnyValue(x => x.Display).Select(x => x == Zoom.True).ToProperty(this, x => x.IsTrueSize);

        _pictureSize = this
            .WhenAnyValue(x => x.Picture, x => x.WindowSize, x => x.Display)
            .Select(_ => SizePicture())
            .ToProperty(this, x => x.PictureSize);

        _position = this
            .WhenAnyValue(x => x.PictureSize, x => x.Offset, x => x.WindowSize)
            .Select(x => PositionPicture())
            .ToProperty(this, x => x.PicturePosition);
    }

    [Reactive] private partial int Index { get; set; }
    [Reactive] private partial (int x, int y) Offset { get; set; }
    [Reactive] private partial Zoom Display { get; set; }
    [Reactive] private partial int Frame { get; set; }

    LoadedImage CurrentImage => _dirImages[Index].image.Value;

    public string Title => _title.Value;
    public Bitmap Picture => _picture.Value;
    public bool IsFill => _isFill.Value;
    public bool IsFit => _isFit.Value;
    public bool IsTrueSize => _isTrueSize.Value;
    public Size WindowSize => _windowSize.Value;
    public Size PictureSize => _pictureSize.Value;
    public Point PicturePosition => _position.Value;
    public ReactiveCommand<Browse, RxVoid> BrowseCommand { get; }
    public ReactiveCommand<Direction, RxVoid> PanCommand { get; }
    public ReactiveCommand<Zoom, RxVoid> ZoomCommand { get; }
    public ReactiveCommand<Size, Size> ResizeCommand { get; }

    void StartAnimation()
    {
        _animation.Stop();
        Frame = 0;
        if (!CurrentImage.IsAnimated) return;
        _animation.Interval = CurrentImage.Durations[0];
        _animation.Start();
    }

    void NextFrame()
    {
        Frame = (Frame + 1) % CurrentImage.Frames.Length;
        _animation.Interval = CurrentImage.Durations[Frame];
    }

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

    static (int index, (string, Lazy<LoadedImage>)[] dirImages) GetDirImageIndex(string initialPath)
    {
        string[] ext = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp"];
        var fullPath = Path.GetFullPath(initialPath);
        var directory = Path.GetDirectoryName(fullPath)!;
        // Natural order, as file managers show it: "img2" before "img10".
        var byName = StringComparer.Create(CultureInfo.CurrentCulture, CompareOptions.IgnoreCase | CompareOptions.NumericOrdering);
        var imagePaths = Directory
            .GetFiles(directory)
            .Where(file => ext.Any(x => Path.GetExtension(file).Equals(x, StringComparison.InvariantCultureIgnoreCase)))
            .OrderBy(Path.GetFileName, byName)
            .ToArray();
        if (imagePaths.Length == 0)
            throw new ArgumentException($"no images found in {directory}");
        // Not one of the listed images (e.g. an unsupported file): start at the first one.
        var index = Math.Max(Array.IndexOf(imagePaths, fullPath), 0);
        var images = imagePaths
            .Select(path => (path, new Lazy<LoadedImage>(() => ImageLoader.Load(path))))
            .ToArray();
        return (index, images);
    }
}