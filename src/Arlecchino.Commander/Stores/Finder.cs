using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
/// The search behind <c>Find file</c>: it walks down from where the panel is looking, on a disk or on
/// a server, matching names against a shell pattern and, when asked, the text inside them. It runs
/// off the drawing thread and can be stopped, because a search over a server is a long walk.
/// </summary>
public sealed class Finder : IArlecchinoStore
{
    private const int Most = 5000;
    private const int Batch = 32;
    private const int Chunk = 64 * 1024;
    private const long Largest = 32L * 1024 * 1024;

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

    /// <summary>How many folders have been looked in so far.</summary>
    public int Looked { get; private set; }

    /// <summary>
    /// Starts a search. Everything it needs is copied out first, so the panel is free to move on
    /// while it runs.
    /// </summary>
    /// <param name="source">Where to look.</param>
    /// <param name="folder">The folder to start from.</param>
    /// <param name="pattern">A shell pattern the name must fit.</param>
    /// <param name="content">Text the file must hold, or empty to ask nothing of the contents.</param>
    /// <param name="done">Called on the drawing thread when the walk has ended.</param>
    public void Start(IFileSource source, string folder, string pattern, string content, Action done)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(done);

        if (IsRunning)
        {
            _state.Output = "A search is still running";
            return;
        }

        var cancelling = new CancellationTokenSource();

        _cancelling = cancelling;
        IsRunning = true;
        Looked = 0;
        Root = folder;
        What = content.Length == 0 ? $"{pattern} under {folder}" : $"{pattern} holding {content} under {folder}";

        Found.Clear();

        _ = Searching();

        async Task Searching()
        {
            await WalkAsync(source, folder, pattern, content, cancelling.Token).ConfigureAwait(false);

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
    /// <param name="content">Text the file must hold, or empty.</param>
    /// <param name="token">Stops the walk.</param>
    private async Task WalkAsync(
        IFileSource source,
        string folder,
        string pattern,
        string content,
        CancellationToken token)
    {
        var pending = new Stack<string>();
        var batch = new List<Hit>();
        var looked = 0;
        var hits = 0;

        pending.Push(folder);

        while (pending.Count > 0 && !token.IsCancellationRequested && hits < Most)
        {
            var here = pending.Pop();
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
                    pending.Push(entry.Path);
                    continue;
                }

                if (!Glob.Matches(entry.Name, pattern) ||
                    (content.Length > 0 && !await HoldsAsync(source, entry, content, token).ConfigureAwait(false)))
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

    /// <summary>
    /// Whether a file holds the text. It is read in chunks with the tail of one kept in front of the
    /// next, so a match that straddles the join is still a match, and anything enormous is left alone.
    /// </summary>
    /// <param name="source">Where the file is.</param>
    /// <param name="entry">The file.</param>
    /// <param name="content">The text to look for.</param>
    /// <param name="token">Stops the read.</param>
    /// <returns><c>true</c> when the text is in there.</returns>
    private static async Task<bool> HoldsAsync(
        IFileSource source,
        FileEntry entry,
        string content,
        CancellationToken token)
    {
        if (entry.Size > Largest)
        {
            return false;
        }

        try
        {
            await using var stream = await source.OpenReadAsync(entry.Path, token).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            var buffer = new char[Chunk];
            var carried = "";

            while (await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false) is var read && read > 0)
            {
                var text = carried + new string(buffer, 0, read);

                if (text.Contains(content, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                carried = text.Length <= content.Length ? text : text[^content.Length..];
            }

            return false;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
                                          or InvalidOperationException or ObjectDisposedException)
        {
            return false;
        }
    }
}
