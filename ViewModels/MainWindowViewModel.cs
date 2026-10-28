using System;
using System.Collections.Generic;
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
    readonly (string path, Func<LoadedImage> load)[] _dirImages;
    // Decoded images are large, so only the current one and its neighbours are kept.
    readonly Dictionary<int, Page> _loaded = new();
    readonly ObservableAsPropertyHelper<string?> _error;
    readonly ObservableAsPropertyHelper<bool> _isFill;
    readonly ObservableAsPropertyHelper<bool> _isFit;
    readonly ObservableAsPropertyHelper<bool> _isTrueSize;
    readonly ObservableAsPropertyHelper<Bitmap?> _picture;
    readonly ObservableAsPropertyHelper<Size> _pictureSize;
    readonly ObservableAsPropertyHelper<Point> _position;
    readonly ObservableAsPropertyHelper<string> _title;
    readonly ObservableAsPropertyHelper<Size> _windowSize;


    public MainWindowViewModel(string initialPath)
    {
        Offset = (0, 0);
        (Index, _dirImages) = GetDirImageIndex(initialPath);
        // An image opened directly that can't be shown is reported by the caller (the help
        // window); ones met while browsing are shown as a message in its place.
        if (CurrentPage.Error is { } error)
            throw new InvalidDataException(error);

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
            .WhenAnyValue(x => x.Index, x => x.Frame, (_, frame) => CurrentPage.Image?.Frames[frame % CurrentPage.Image.Frames.Length])
            .ToProperty(this, x => x.Picture);

        _error = this
            .WhenAnyValue(x => x.Index)
            .Select(_ => CurrentPage.Error is { } error ? $"Can't show this image: {error}" : null)
            .ToProperty(this, x => x.Error);

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

    Page CurrentPage => GetPage(Index);

    public string Title => _title.Value;
    public Bitmap? Picture => _picture.Value;
    public string? Error => _error.Value;
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

    Page GetPage(int index)
    {
        if (!_loaded.TryGetValue(index, out var page))
        {
            try
            {
                page = new(_dirImages[index].load(), null);
            }
            catch (Exception e)
            {
                // A viewer shouldn't die on one bad file, whatever the decoder threw.
                page = new(null, e.Message);
            }

            _loaded[index] = page;
        }

        foreach (var far in _loaded.Keys.Where(i => Math.Abs(i - index) > 1).ToArray())
        {
            foreach (var frame in _loaded[far].Image?.Frames ?? []) frame.Dispose();
            _loaded.Remove(far);
        }

        return page;
    }

    void StartAnimation()
    {
        _animation.Stop();
        Frame = 0;
        if (CurrentPage.Image is not { IsAnimated: true } image) return;
        _animation.Interval = image.Durations[0];
        _animation.Start();
    }

    void NextFrame()
    {
        if (CurrentPage.Image is not { } image) return;
        Frame = (Frame + 1) % image.Frames.Length;
        _animation.Interval = image.Durations[Frame];
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
        if (Picture is null) return default;
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

    /// <summary>An image, or why it couldn't be loaded.</summary>
    record Page(LoadedImage? Image, string? Error);

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

    static (int index, (string, Func<LoadedImage>)[] dirImages) GetDirImageIndex(string initialPath)
    {
        var fullPath = Path.GetFullPath(initialPath);
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            throw new FileNotFoundException("no such file or folder");
        // Natural order, as file managers show it: "img2" before "img10".
        var byName = StringComparer.Create(CultureInfo.CurrentCulture, CompareOptions.IgnoreCase | CompareOptions.NumericOrdering);

        // An archive (e.g. a comic book) opens at its first page.
        if (ImageArchive.IsArchive(fullPath) && File.Exists(fullPath))
        {
            var pages = ImageArchive.ListImages(fullPath, byName);
            if (pages.Length == 0)
                throw new ArgumentException($"no images found in {fullPath}");
            var archiveName = Path.GetFileName(fullPath);
            return (0, pages
                .Select(page => ($"{archiveName} › {page}", (Func<LoadedImage>)(() => ImageArchive.Load(fullPath, page))))
                .ToArray());
        }

        // A folder opens at its first image.
        var directory = Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath)!;
        var imagePaths = Directory
            .GetFiles(directory)
            .Where(ImageLoader.IsImage)
            .OrderBy(Path.GetFileName, byName)
            .ToArray();
        if (imagePaths.Length == 0)
            throw new ArgumentException($"no images found in {directory}");
        // An existing file that isn't one of the listed images (e.g. an unsupported type):
        // start at the first image beside it.
        var index = Math.Max(Array.IndexOf(imagePaths, fullPath), 0);
        var images = imagePaths
            .Select(path => (path, (Func<LoadedImage>)(() => ImageLoader.Load(path))))
            .ToArray();
        return (index, images);
    }
}