using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Files.Watching;

/// <summary>
/// A folder on a server, read again every so often. What came back is boiled down to one number and
/// compared with the last, so an unchanged folder costs a listing and no more.
/// </summary>
internal sealed class PollWatch : IDisposable
{
    private readonly CancellationTokenSource _stopping = new();

    private long _stamp;

    private PollWatch(long stamp) => _stamp = stamp;

    /// <summary>Starts reading a folder over and over, from what the panel has just read as the first word.</summary>
    /// <param name="source">Who to ask.</param>
    /// <param name="folder">Which folder.</param>
    /// <param name="hidden">Whether the hidden files count, which is what the panel is showing.</param>
    /// <param name="interval">How long to wait between readings.</param>
    /// <param name="stamp">What the panel already has, from <see cref="Print"/>.</param>
    /// <param name="carrying">Whether files are being carried, in which case the reading is passed over.</param>
    /// <param name="changed">Called on another thread when the folder is no longer that.</param>
    /// <returns>The watch, which stops when it is disposed.</returns>
    public static PollWatch Over(
        IFileSource source,
        string folder,
        bool hidden,
        TimeSpan interval,
        long stamp,
        Func<bool> carrying,
        Action changed)
    {
        var watch = new PollWatch(stamp);

        _ = watch.ReadingAsync(source, folder, hidden, interval, carrying, changed);

        return watch;
    }

    /// <summary>
    /// A listing as one number, which is what two readings are compared by. Names, sizes and times count,
    /// so a file written into is noticed as well as one made; the order they came in does not.
    /// </summary>
    /// <param name="entries">What was read.</param>
    /// <returns>The number.</returns>
    public static long Print(IReadOnlyList<FileEntry> entries)
    {
        var print = (long)entries.Count;

        foreach (var entry in entries)
        {
            unchecked
            {
                print += HashCode.Combine(entry.Name, entry.Size, entry.Modified, entry.IsFolder);
            }
        }

        return print;
    }

    /// <summary>
    /// Takes what the panel has just read as the newest word, so that a reading it did itself is not
    /// reported back to it as news.
    /// </summary>
    /// <param name="print">The listing, from <see cref="Print"/>.</param>
    public void Saw(long print) => Interlocked.Exchange(ref _stamp, print);

    /// <summary>Stops the reading, after however long the one in flight takes to come back.</summary>
    public void Dispose()
    {
        _stopping.Cancel();
        _stopping.Dispose();
    }

    /// <summary>
    /// The loop itself: waits, reads, compares, and reports a folder that is no longer what it was. A
    /// reading that failed is passed over, since it says nothing about what is in the folder.
    /// </summary>
    /// <param name="source">Who to ask.</param>
    /// <param name="folder">Which folder.</param>
    /// <param name="hidden">Whether the hidden files count.</param>
    /// <param name="interval">How long to wait between readings.</param>
    /// <param name="carrying">
    /// Whether files are being carried, in which case the reading is passed over. FTP answers over the one
    /// connection a transfer is talking on, and the work finishing reads the panels again anyway.
    /// </param>
    /// <param name="changed">Called when the folder is no longer what it was.</param>
    private async Task ReadingAsync(
        IFileSource source,
        string folder,
        bool hidden,
        TimeSpan interval,
        Func<bool> carrying,
        Action changed)
    {
        var token = _stopping.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(interval, token).ConfigureAwait(false);

                if (carrying())
                {
                    continue;
                }

                var readCount = await Listing.ReadAsync(source, folder, hidden).ConfigureAwait(false);

                if (readCount.Error.Length > 0 || token.IsCancellationRequested)
                {
                    continue;
                }

                var print = Print(readCount.Entries);

                if (Interlocked.Exchange(ref _stamp, print) != print)
                {
                    changed();
                }
            }
        }
        catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException) { }
    }
}
