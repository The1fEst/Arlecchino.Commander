using System;
using System.Collections.Generic;
using System.Threading;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Files.Watching;

/// <summary>
/// Keeps a panel on the folder it is showing: the machine's own watch where the source has one, a reading
/// every so often where it has not. A burst of changes is held and passed on as one.
/// </summary>
public sealed class FolderWatch : IDisposable
{
    private const int SettleMilliseconds = 400;

    private readonly Func<TimeSpan> _interval;
    private readonly Func<bool> _carrying;
    private readonly Action _changed;
    private readonly Timer _settling;

    private IFileSource? _source;
    private string _folder = "";
    private bool _hidden;
    private IDisposable? _watching;
    private PollWatch? _polling;
    private int _pending;
    private bool _stopped;

    /// <summary>Sets a watch up over one panel.</summary>
    /// <param name="interval">
    /// How often a source with nothing to watch with should be read again, asked afresh every time so that
    /// changing the setting is in force at once. Nothing at all turns the watching off.
    /// </param>
    /// <param name="carrying">
    /// Asked before a source is read again whether files are being carried. One is worth waiting out: a
    /// server has one connection to answer over, and the work finishing reads the panels anyway.
    /// </param>
    /// <param name="changed">
    /// Called on another thread when the folder is no longer what it was. Whoever is told must get onto the
    /// drawing thread before acting.
    /// </param>
    public FolderWatch(Func<TimeSpan> interval, Func<bool> carrying, Action changed)
    {
        _interval = interval;
        _carrying = carrying;
        _changed = changed;
        _settling = new(_ => Settled(), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Watches what the panel has just read. Called after every reading: the same folder read again only
    /// tells the watch what is there now, and another folder starts the watching over.
    /// </summary>
    /// <param name="source">Where the panel is looking.</param>
    /// <param name="folder">Which folder.</param>
    /// <param name="hidden">Whether the panel is showing the hidden files.</param>
    /// <param name="entries">What the reading came back with.</param>
    public void Follow(IFileSource source, string folder, bool hidden, IReadOnlyList<FileEntry> entries)
    {
        if (_stopped)
        {
            return;
        }

        var interval = _interval();

        if (interval <= TimeSpan.Zero)
        {
            Stop();

            return;
        }

        if (IsOn(source, folder, hidden))
        {
            _polling?.Saw(PollWatch.Print(entries));

            return;
        }

        Stop();

        _source = source;
        _folder = folder;
        _hidden = hidden;

        if (source is IWatchesFolder told && told.Watch(folder, Told) is { } watching)
        {
            _watching = watching;

            return;
        }

        _polling = PollWatch.Over(source, folder, hidden, interval, PollWatch.Print(entries), _carrying, Told);
    }

    /// <summary>Stops watching, leaving the watch able to start again, as a tab stepped away from does.</summary>
    public void Stop()
    {
        _watching?.Dispose();
        _watching = null;

        _polling?.Dispose();
        _polling = null;

        _source = null;
        _folder = "";
    }

    /// <summary>Stops watching for good, so that nothing is said after the panel has gone.</summary>
    public void Dispose()
    {
        _stopped = true;

        Stop();

        _settling.Dispose();
    }

    /// <summary>Whether this is already the folder being watched.</summary>
    /// <param name="source">Where the panel is looking.</param>
    /// <param name="folder">Which folder.</param>
    /// <param name="hidden">Whether the panel is showing the hidden files.</param>
    /// <returns><c>true</c> when the watch is on and on that.</returns>
    private bool IsOn(IFileSource source, string folder, bool hidden) =>
        (_watching is not null || _polling is not null) &&
        ReferenceEquals(_source, source) &&
        _hidden == hidden &&
        string.Equals(_folder, folder, StringComparison.Ordinal);

    /// <summary>
    /// Something changed. The first word starts the moment the news is held for, and everything said during
    /// it is that same news — a copy landing file by file must not be a reading per file.
    /// </summary>
    private void Told()
    {
        if (!_stopped && Interlocked.Exchange(ref _pending, 1) == 0)
        {
            _settling.Change(SettleMilliseconds, Timeout.Infinite);
        }
    }

    /// <summary>The moment is up: the news goes on, and the next word starts another moment.</summary>
    private void Settled()
    {
        Volatile.Write(ref _pending, 0);

        if (!_stopped)
        {
            _changed();
        }
    }
}
