using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Atoms;
using Arlecchino.Atoms.Local;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;
using Arlecchino.State;

namespace Arlecchino.Commander.Stores;

/// <summary>Where a file was found, and the entry itself.</summary>
/// <param name="Folder">The folder holding it.</param>
/// <param name="Entry">The file.</param>
public sealed record Hit(string Folder, FileEntry Entry);

/// <summary>
/// The search behind <c>Find file</c>, which walks down from where the panel is looking and matches names
/// against what was typed. It runs off the drawing thread.
/// </summary>
public sealed class Finder : IArlecchinoStore
{
    private const int Most = 5000;
    private const int Batch = 32;

    private readonly ArlecchinoState _state;

    private CancellationTokenSource? _cancelling;

    public Finder(ArlecchinoState state) => _state = state;

    /// <summary>
    /// What the walk has found so far. A list atom rather than a list, so that a batch landing on the
    /// drawing thread marks the frame stale by itself and the results appear as they are found.
    /// </summary>
    public LocalAtomsList<Hit> Found { get; } = new();

    public bool IsRunning { get; private set; }

    /// <summary>What was asked for, as it is shown at the top of the results.</summary>
    public string What { get; private set; } = "";

    /// <summary>The folder the walk started from, which the results are shown relative to.</summary>
    public string Root { get; private set; } = "";

    /// <summary>Where the walk is being run, which is what <see cref="Root"/> is written against.</summary>
    public IFileSource? Source { get; private set; }

    /// <summary>How many folders have been looked in so far.</summary>
    public int Looked { get; private set; }

    /// <summary>
    /// Starts a search. Everything it needs is copied out first, so the panel is free to move on
    /// while it runs.
    /// </summary>
    /// <param name="source">Where to look.</param>
    /// <param name="folder">The folder to start from.</param>
    /// <param name="pattern">What the name must hold, or a shell pattern it must fit when one was spelled.</param>
    /// <param name="done">Called on the drawing thread when the walk has ended.</param>
    public void Start(IFileSource source, string folder, string pattern, Action done)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(done);

        if (IsRunning)
        {
            _state.Output = Loc(LocString.SaidSearchRunning);
            return;
        }

        var cancelling = new CancellationTokenSource();
        var wanted = Glob.Anywhere(pattern);

        _cancelling = cancelling;
        IsRunning = true;
        Looked = 0;
        Root = folder;
        Source = source;
        What = wanted;

        Found.Clear();

        _ = Searching();

        async Task Searching()
        {
            await WalkAsync(source, folder, wanted, cancelling.Token).ConfigureAwait(false);

            FrameThread.Post(() =>
            {
                IsRunning = false;
                _cancelling = null;

                cancelling.Dispose();
                done();

                _state.Invalidate();
            });
        }
    }

    public void Stop() => _cancelling?.Cancel();

    /// <summary>
    /// Walks the tree. What it finds is handed to the drawing thread in batches rather than added
    /// here, so the results list is only ever touched by the thread that draws it.
    /// </summary>
    /// <param name="source">Where to look.</param>
    /// <param name="folder">The folder to start from.</param>
    /// <param name="pattern">The shell pattern.</param>
    /// <param name="token">Stops the walk.</param>
    private async Task WalkAsync(IFileSource source, string folder, string pattern, CancellationToken token)
    {
        var pending = new Queue<string>();
        var batch = new List<Hit>();
        var looked = 0;
        var hits = 0;

        pending.Enqueue(folder);

        while (pending.Count > 0 && !token.IsCancellationRequested && hits < Most)
        {
            var here = pending.Dequeue();
            var seen = ++looked;

            FrameThread.Post(() => Looked = seen);

            foreach (var entry in await ListedAsync(source, here, token).ConfigureAwait(false))
            {
                if (entry.IsParent)
                {
                    continue;
                }

                if (entry.IsFolder)
                {
                    pending.Enqueue(entry.Path);
                    continue;
                }

                if (!Glob.Matches(entry.Name, pattern))
                {
                    continue;
                }

                batch.Add(new(here, entry));
                hits++;
            }

            Hand(batch, Batch);
        }

        Hand(batch, 1);
    }

    private void Hand(List<Hit> batch, int least)
    {
        if (batch.Count < least)
        {
            return;
        }

        var carried = batch.ToArray();

        batch.Clear();

        FrameThread.Post(() => Found.Add(carried));
    }

    private static async Task<IReadOnlyList<FileEntry>> ListedAsync(
        IFileSource source,
        string folder,
        CancellationToken token)
    {
        try
        {
            return await source.ListAsync(folder, showHidden: true, token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
                                          or InvalidOperationException)
        {
            return [];
        }
    }
}
