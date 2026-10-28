using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        Next,
        First,
        Last
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

    readonly DispatcherTimer _animation = new() { Interval = TimeSpan.FromMilliseconds(15) };
    readonly Stopwatch _sinceShown = new();
    readonly (string label, string name, Func<LoadedImage> load)[] _dirImages;
    readonly PageCache _pages;
    // Which way the reader is going (+1 or -1), so the next pages are loaded first.
    int _direction = 1;
    // Bumped whenever what should be on screen changes, so a slow load that finishes
    // after the reader has moved on is ignored.
    int _showing;
    readonly ObservableAsPropertyHelper<string?> _error;
    readonly ObservableAsPropertyHelper<bool> _isFill;
    readonly ObservableAsPropertyHelper<bool> _isFit;
    readonly ObservableAsPropertyHelper<bool> _isTrueSize;
    readonly ObservableAsPropertyHelper<Bitmap?> _picture;
    readonly ObservableAsPropertyHelper<Bitmap?> _picture2;
    readonly ObservableAsPropertyHelper<Size> _picture1Size;
    readonly ObservableAsPropertyHelper<Size> _picture2Size;
    readonly ObservableAsPropertyHelper<Size> _pictureSize;
    readonly ObservableAsPropertyHelper<Point> _position;
    readonly ObservableAsPropertyHelper<string> _counter;
    readonly ObservableAsPropertyHelper<string> _caption;
    readonly ObservableAsPropertyHelper<string?> _dimensions;
    readonly ObservableAsPropertyHelper<string> _windowTitle;
    readonly ObservableAsPropertyHelper<bool> _canGoBack;
    readonly ObservableAsPropertyHelper<bool> _canGoForward;
    readonly ObservableAsPropertyHelper<Size> _windowSize;


    public MainWindowViewModel(string initialPath)
    {
        Offset = (0, 0);
        bool rightToLeft;
        (Index, _dirImages, rightToLeft) = GetDirImageIndex(initialPath);
        RightToLeft = rightToLeft;
        CoverAlone = true;
        _pages = new(_dirImages.Select(x => x.load).ToArray());
        // An image opened directly that can't be shown is reported by the caller (the help
        // window); ones met while browsing are shown as a message in its place.
        var first = _pages.LoadNow(Index);
        if (first.Error is { } error)
            throw new InvalidDataException(error);
        Shown = [first];

        Display = Zoom.True;
        BrowseCommand = ReactiveCommand.Create<Browse>(BrowseImage);
        PanCommand = ReactiveCommand.Create<Direction>(PanImage);
        ZoomCommand = ReactiveCommand.Create<Zoom>(ZoomImage);
        PagesCommand = ReactiveCommand.Create<bool>(SetSpread);
        ReadingDirectionCommand = ReactiveCommand.Create(() => { RightToLeft = !RightToLeft; });
        ResizeCommand = ReactiveCommand.Create<Size, Size>(x => x);

        _windowSize = ResizeCommand
            .Throttle(TimeSpan.FromMilliseconds(100))
            .ToProperty(this, x => x.WindowSize);

        var visible = this.WhenAnyValue(x => x.Index, x => x.Spread, x => x.CoverAlone, (_, _, _) => PagesAt(Index));
        _counter = visible
            .Select(pages => $"{string.Join("–", pages.Select(page => page + 1))} / {_dirImages.Length}")
            .ToProperty(this, x => x.Counter);
        _caption = visible
            .Select(pages => string.Join(" | ", pages.Select((page, i) => i == 0 ? _dirImages[page].label : _dirImages[page].name)))
            .ToProperty(this, x => x.Caption);
        _canGoBack = visible.Select(pages => pages[0] > 0).ToProperty(this, x => x.CanGoBack);
        _canGoForward = visible.Select(pages => pages[^1] < _dirImages.Length - 1).ToProperty(this, x => x.CanGoForward);
        _windowTitle = visible
            .Select(pages => $"{_dirImages[pages[0]].name} — EyeBrowse")
            .ToProperty(this, x => x.WindowTitle);

        // Pages are laid out left to right; manga reads right to left.
        _picture = this
            .WhenAnyValue(x => x.Shown, x => x.RightToLeft, x => x.Elapsed, (shown, rtl, elapsed) => FrameAt(OnScreen(shown, rtl, 0), elapsed))
            .ToProperty(this, x => x.Picture);
        _picture2 = this
            .WhenAnyValue(x => x.Shown, x => x.RightToLeft, x => x.Elapsed, (shown, rtl, elapsed) => FrameAt(OnScreen(shown, rtl, 1), elapsed))
            .ToProperty(this, x => x.Picture2);

        _error = this
            .WhenAnyValue(x => x.Shown)
            .Select(shown => shown?.Select(page => page.Error).OfType<string>().FirstOrDefault() is { } error
                ? $"Can't show this image: {error}"
                : null)
            .ToProperty(this, x => x.Error);

        _animation.Tick += (_, _) => Elapsed = _sinceShown.Elapsed;
        this.WhenAnyValue(x => x.Shown).Subscribe(_ => StartAnimation());
        this.WhenAnyValue(x => x.Index, x => x.Spread, x => x.CoverAlone, (_, _, _) => RxVoid.Default).Subscribe(_ => ShowPages());

        // Pixel size, for a single image (it explains what true size shows).
        _dimensions = this
            .WhenAnyValue(x => x.Picture, x => x.Picture2,
                (picture, second) => picture is null || second is not null ? null : $"{picture.PixelSize.Width} × {picture.PixelSize.Height}")
            .ToProperty(this, x => x.Dimensions);
        ShowOverlay = true;
        ToggleOverlayCommand = ReactiveCommand.Create(() => { ShowOverlay = !ShowOverlay; });

        _isFill = this.WhenAnyValue(x => x.Display).Select(x => x == Zoom.Fill).ToProperty(this, x => x.IsFill);
        _isFit = this.WhenAnyValue(x => x.Display).Select(x => x == Zoom.Fit).ToProperty(this, x => x.IsFit);
        _isTrueSize = this.WhenAnyValue(x => x.Display).Select(x => x == Zoom.True).ToProperty(this, x => x.IsTrueSize);

        _picture1Size = this
            .WhenAnyValue(x => x.Picture, x => x.Picture2, x => x.WindowSize, x => x.Display, (_, _, _, _) => DisplaySize(Picture))
            .ToProperty(this, x => x.Picture1Size);
        _picture2Size = this
            .WhenAnyValue(x => x.Picture, x => x.Picture2, x => x.WindowSize, x => x.Display, (_, _, _, _) => DisplaySize(Picture2))
            .ToProperty(this, x => x.Picture2Size);
        _pictureSize = this
            .WhenAnyValue(x => x.Picture1Size, x => x.Picture2Size,
                (a, b) => new Size(a.Width + b.Width, Math.Max(a.Height, b.Height)))
            .ToProperty(this, x => x.PictureSize);

        _position = this
            .WhenAnyValue(x => x.PictureSize, x => x.Offset, x => x.WindowSize)
            .Select(x => PositionPicture())
            .ToProperty(this, x => x.PicturePosition);
    }

    // The first page on screen: with two pages, the lower-numbered one.
    [Reactive] private partial int Index { get; set; }
    [Reactive] private partial (int x, int y) Offset { get; set; }
    [Reactive] private partial Zoom Display { get; set; }
    [Reactive] public partial bool Spread { get; private set; }
    // Two-page pairing: the cover alone then 2-3, 4-5 (as printed), or 1-2, 3-4.
    [Reactive] private partial bool CoverAlone { get; set; }
    [Reactive] public partial bool RightToLeft { get; private set; }
    // The pages on screen, in reading order: null while one is still loading.
    [Reactive] private partial Page[]? Shown { get; set; }
    // Time since the pages appeared, which picks each animated page's frame.
    [Reactive] private partial TimeSpan Elapsed { get; set; }
    [Reactive] public partial bool IsLoading { get; private set; }
    [Reactive] public partial bool ShowOverlay { get; private set; }

    // "3 / 12", the image's name (and the second page's, with two), and its pixel size.
    public string Counter => _counter.Value;
    public string Caption => _caption.Value;
    public string? Dimensions => _dimensions.Value;
    public string WindowTitle => _windowTitle.Value;
    // Whether Q / E have anywhere to go.
    public bool CanGoBack => _canGoBack.Value;
    public bool CanGoForward => _canGoForward.Value;
    // The left and right page; only Picture is used for a single page.
    public Bitmap? Picture => _picture.Value;
    public Bitmap? Picture2 => _picture2.Value;
    public string? Error => _error.Value;
    public bool IsFill => _isFill.Value;
    public bool IsFit => _isFit.Value;
    public bool IsTrueSize => _isTrueSize.Value;
    public Size WindowSize => _windowSize.Value;
    public Size Picture1Size => _picture1Size.Value;
    public Size Picture2Size => _picture2Size.Value;
    // Both pages together.
    public Size PictureSize => _pictureSize.Value;
    public Point PicturePosition => _position.Value;
    public ReactiveCommand<Browse, RxVoid> BrowseCommand { get; }
    public ReactiveCommand<Direction, RxVoid> PanCommand { get; }
    public ReactiveCommand<Zoom, RxVoid> ZoomCommand { get; }
    // false: one page; true: two pages, or if already showing two, shift the pairing.
    public ReactiveCommand<bool, RxVoid> PagesCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ReadingDirectionCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ToggleOverlayCommand { get; }
    public ReactiveCommand<Size, Size> ResizeCommand { get; }

    /// <summary>The pages shown together with page <paramref name="index"/>, in page order.</summary>
    int[] PagesAt(int index)
    {
        if (!Spread) return [index];
        var first = index - (((index - (CoverAlone ? 1 : 0)) % 2 + 2) % 2);
        return new[] { first, first + 1 }.Where(i => i >= 0 && i < _dirImages.Length).ToArray();
    }

    async void ShowPages()
    {
        var showing = ++_showing;
        var visible = PagesAt(Index);
        var loads = visible.Select(_pages.Get).ToArray();
        _pages.Keep(Neighbourhood(visible));

        if (loads.Any(load => !load.IsCompleted))
        {
            Shown = null;
            ShowLoadingIfSlow(showing);
        }

        Page[] loaded;
        try
        {
            loaded = await Task.WhenAll(loads);
        }
        catch (OperationCanceledException)
        {
            return; // moved on before it loaded
        }

        if (showing != _showing) return;
        Shown = loaded;
        IsLoading = false;
    }

    // The pages on screen, then the ones a step ahead (loaded first) and a step back.
    IEnumerable<int> Neighbourhood(int[] visible)
    {
        var ahead = _direction > 0 ? visible[^1] + 1 : visible[0] - 1;
        var behind = _direction > 0 ? visible[0] - 1 : visible[^1] + 1;
        return visible.Concat(Around(ahead)).Concat(Around(behind));

        IEnumerable<int> Around(int index) => index >= 0 && index < _dirImages.Length ? PagesAt(index) : [];
    }

    // A message only for a noticeable wait, so quick loads don't flicker.
    async void ShowLoadingIfSlow(int showing)
    {
        await Task.Delay(150);
        if (showing == _showing && Shown is null) IsLoading = true;
    }

    static Page? OnScreen(Page[]? shown, bool rightToLeft, int slot)
    {
        if (shown is null || slot >= shown.Length) return null;
        return rightToLeft ? shown[shown.Length - 1 - slot] : shown[slot];
    }

    static Bitmap? FrameAt(Page? page, TimeSpan elapsed)
    {
        if (page?.Image is not { } image) return null;
        if (!image.IsAnimated) return image.Frames[0];
        var into = elapsed.Ticks % image.Durations.Sum(d => d.Ticks);
        for (var frame = 0;; frame++)
        {
            if (into < image.Durations[frame].Ticks) return image.Frames[frame];
            into -= image.Durations[frame].Ticks;
        }
    }

    void StartAnimation()
    {
        _sinceShown.Restart();
        Elapsed = TimeSpan.Zero;
        if (Shown?.Any(page => page.Image?.IsAnimated == true) == true)
            _animation.Start();
        else
            _animation.Stop();
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


    // The on-screen size of one page. Two pages are scaled to the same height and fitted
    // as one picture; at true size each keeps its own pixel size.
    Size DisplaySize(Bitmap? picture)
    {
        if (picture is null) return default;
        if (Display == Zoom.True) return picture.Size;

        var shown = new[] { Picture, Picture2 }.OfType<Bitmap>().ToArray();
        var height = shown.Max(p => p.Size.Height);
        var together = new Size(shown.Sum(p => p.Size.Width * height / p.Size.Height), height);
        var scale = Scaled(together).Height / height;
        return new(picture.Size.Width * height / picture.Size.Height * scale, height * scale);
    }

    Size Scaled(Size picture)
    {
        var ratio = picture.AspectRatio;
        return Display switch
        {
            Zoom.Fill =>
                WindowSize.Height * ratio <= WindowSize.Width
                    ? new(WindowSize.Width, WindowSize.Width / ratio)
                    : new(WindowSize.Height * ratio, WindowSize.Height),
            Zoom.Fit =>
                WindowSize.Height * ratio <= WindowSize.Width
                    ? new(WindowSize.Height * ratio, WindowSize.Height)
                    : new(WindowSize.Width, WindowSize.Width / ratio),
            _ => picture
        };
    }


    void BrowseImage(Browse browse)
    {
        // From the first page the reader can only go forward, and from the last only back.
        _direction = browse is Browse.Previous or Browse.Last ? -1 : 1;
        if (browse is Browse.First or Browse.Last)
        {
            Index = PagesAt(browse == Browse.First ? 0 : _dirImages.Length - 1)[0];
            return;
        }

        var visible = PagesAt(Index);
        var next = browse == Browse.Previous ? visible[0] - 1 : visible[^1] + 1;
        if (next >= 0 && next < _dirImages.Length)
            Index = PagesAt(next)[0];
    }

    void SetSpread(bool spread)
    {
        // Shifting the pairing (2 again) keeps the page that was first on screen visible.
        if (spread && Spread)
            CoverAlone = !CoverAlone;
        Spread = spread;
        Index = PagesAt(Index)[0];
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

    static (int index, (string label, string name, Func<LoadedImage> load)[] dirImages, bool rightToLeft) GetDirImageIndex(string initialPath)
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
                .Select(page => ($"{archiveName} › {page}", Path.GetFileName(page), (Func<LoadedImage>)(() => ImageArchive.Load(fullPath, page))))
                .ToArray(), ImageArchive.IsRightToLeft(fullPath));
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
            .Select(path => (Path.GetFileName(path), Path.GetFileName(path), (Func<LoadedImage>)(() => ImageLoader.Load(path))))
            .ToArray();
        return (index, images, false);
    }
}