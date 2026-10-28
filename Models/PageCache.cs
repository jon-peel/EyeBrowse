using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EyeBrowse.Models;

/// <summary>An image, or why it couldn't be loaded.</summary>
public record Page(LoadedImage? Image, string? Error);

/// <summary>
/// Decodes images in the background ahead of the reader, and keeps only the ones near
/// the current position: decoded images are large. Used from the UI thread only.
/// </summary>
public sealed class PageCache(Func<LoadedImage>[] loaders)
{
    readonly Dictionary<int, (Task<Page> Task, CancellationTokenSource Cancel)> _pages = new();
    // One decode at a time: enough to stay a step ahead, and decoding a huge image takes
    // several times its final size while it runs.
    readonly SemaphoreSlim _decoders = new(1);

    public int Count => loaders.Length;

    /// <summary>Loads an image on the calling thread, e.g. the first one, whose error the caller reports.</summary>
    public Page LoadNow(int index)
    {
        var page = Load(index);
        _pages[index] = (Task.FromResult(page), new());
        return page;
    }

    /// <summary>The image at <paramref name="index"/>, starting to load it if needed.</summary>
    public Task<Page> Get(int index)
    {
        if (_pages.TryGetValue(index, out var entry)) return entry.Task;

        var cancel = new CancellationTokenSource();
        var token = cancel.Token;
        var task = Task.Run(async () =>
        {
            await _decoders.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();
                return Load(index);
            }
            finally
            {
                _decoders.Release();
            }
        }, token);
        _pages[index] = (task, cancel);
        return task;
    }

    /// <summary>
    /// Starts loading <paramref name="wanted"/> in the order given (most needed first), and
    /// drops everything else, freeing its pixels once any decode in progress finishes.
    /// </summary>
    public void Keep(IEnumerable<int> wanted)
    {
        var keep = wanted.Where(i => i >= 0 && i < Count).Distinct().ToList();
        foreach (var index in keep) Get(index);

        foreach (var index in _pages.Keys.Except(keep).ToArray())
        {
            var (task, cancel) = _pages[index];
            _pages.Remove(index);
            cancel.Cancel();
            task.ContinueWith(done =>
            {
                if (done.IsCompletedSuccessfully)
                    foreach (var frame in done.Result.Image?.Frames ?? []) frame.Dispose();
                cancel.Dispose();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }

    Page Load(int index)
    {
        try
        {
            return new(loaders[index](), null);
        }
        catch (Exception e)
        {
            // A viewer shouldn't die on one bad file, whatever the decoder threw.
            return new(null, e.Message);
        }
    }
}
