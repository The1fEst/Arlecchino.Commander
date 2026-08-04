using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Files.Sources;

namespace Arlecchino.Commander.Files.Work;

/// <summary>How much there is to do, counted before the work starts so a bar has a denominator.</summary>
/// <param name="Files">How many files the sources hold, at every depth.</param>
/// <param name="Folders">How many folders, the sources themselves included.</param>
/// <param name="Bytes">How much the files add up to.</param>
public readonly record struct Tally(int Files, int Folders, long Bytes)
{
    /// <summary>Files and folders together, which is what a delete works through.</summary>
    public int Items => Files + Folders;
}

/// <summary>
/// Copying, moving and deleting. Every one of these waits on something — a disk, a server — so every
/// one of them is awaited rather than run, and the stop key is answered between blocks of a file
/// rather than between files: a single large file used to have to finish before anyone could be told
/// to stop.
/// </summary>
public static class FileTasks
{
    private const int Block = 128 * 1024;

    /// <summary>
    /// Walks the sources without touching them, so the work that follows knows how far along it is. A
    /// folder is counted along with everything under it, which is what makes a bar for a delete of a
    /// deep tree mean anything.
    /// </summary>
    /// <param name="source">Where the entries live.</param>
    /// <param name="entries">What is about to be worked on.</param>
    /// <param name="token">Stops the count when the operation is cancelled before it starts.</param>
    /// <returns>What was found; zeroes when the count was cut short.</returns>
    public static async Task<Tally> MeasureAsync(
        IFileSource source,
        IReadOnlyList<FileEntry> entries,
        CancellationToken token)
    {
        var files = 0;
        var folders = 0;
        var bytes = 0L;

        await SpreadAsync(
                source,
                entries,
                async entry =>
                {
                    if (!entry.IsFolder)
                    {
                        Interlocked.Increment(ref files);
                        Interlocked.Add(ref bytes, entry.Size);

                        return;
                    }

                    Interlocked.Increment(ref folders);

                    var below = await MeasureAsync(source,
                            await ChildrenAsync(source, entry, token).ConfigureAwait(false),
                            token)
                        .ConfigureAwait(false);

                    Interlocked.Add(ref files, below.Files);
                    Interlocked.Add(ref folders, below.Folders);
                    Interlocked.Add(ref bytes, below.Bytes);
                },
                token)
            .ConfigureAwait(false);

        return new(files, folders, bytes);
    }

    /// <summary>
    /// What is inside a folder, without the entry that leads back out of it. A folder that cannot be
    /// read counts as empty here — the work itself will report why when it gets there.
    /// </summary>
    private static async Task<List<FileEntry>> ChildrenAsync(
        IFileSource source,
        FileEntry folder,
        CancellationToken token)
    {
        IReadOnlyList<FileEntry> listed;

        try
        {
            listed = await source.ListAsync(folder.Path, true, token).ConfigureAwait(false);
        }
        catch (Exception error) when (IsExpected(error))
        {
            return [];
        }

        var children = new List<FileEntry>(listed.Count);

        foreach (var child in listed)
        {
            if (!child.IsParent)
            {
                children.Add(child);
            }
        }

        return children;
    }

    public static async Task CopyAsync(
        IFileSource from,
        IReadOnlyList<FileEntry> sources,
        IFileSource to,
        string target,
        Outcome outcome,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(to);

        foreach (var source in sources)
        {
            await CopyOneAsync(from, source, to, to.Combine(target, source.Name), outcome, token)
                .ConfigureAwait(false);
        }
    }

    public static async Task MoveAsync(
        IFileSource from,
        IReadOnlyList<FileEntry> sources,
        IFileSource to,
        string target,
        Outcome outcome,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(to);

        foreach (var source in sources)
        {
            await MoveOneAsync(from, source, to, to.Combine(target, source.Name), outcome, token)
                .ConfigureAwait(false);
        }
    }

    public static Task RenameAsync(IFileSource source, FileEntry entry, string target, Outcome outcome) =>
        MoveOneAsync(source, entry, source, target, outcome, CancellationToken.None);

    public static async Task DeleteAsync(
        IFileSource source,
        IReadOnlyList<FileEntry> entries,
        Outcome outcome,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(entries);

        foreach (var entry in entries)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            await DeleteOneAsync(source, entry, outcome, token).ConfigureAwait(false);
        }
    }

    public static async Task<string?> CreateFolderAsync(
        IFileSource source,
        string parent,
        string name,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            var path = source.Combine(parent, name);

            await source.CreateFolderAsync(path, token).ConfigureAwait(false);

            return path;
        }
        catch (Exception error) when (IsExpected(error))
        {
            return null;
        }
    }

    /// <summary>
    /// Removes one entry. A folder goes in a single request when the source can do that — one
    /// <c>rm -rf</c> on a server instead of a round trip per file — and is walked otherwise, several
    /// children at a time, so the count and the stop key keep working.
    /// </summary>
    private static async Task DeleteOneAsync(
        IFileSource source,
        FileEntry entry,
        Outcome outcome,
        CancellationToken token)
    {
        try
        {
            outcome.Reached(entry.Name);

            if (!entry.IsFolder)
            {
                await source.DeleteAsync(entry, token).ConfigureAwait(false);
                outcome.Counted(0);

                return;
            }

            if (await source.TryDeleteTreeAsync(entry, token).ConfigureAwait(false))
            {
                outcome.Swept();

                return;
            }

            var children = await ChildrenAsync(source, entry, token).ConfigureAwait(false);

            await SpreadAsync(source, children, child => DeleteOneAsync(source, child, outcome, token), token)
                .ConfigureAwait(false);

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (!outcome.Failed)
            {
                await source.DeleteAsync(entry, token).ConfigureAwait(false);
            }

            outcome.CountedFolder();
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (IsExpected(error))
        {
            outcome.Failing(entry.Name, error.Message);
        }
    }

    /// <summary>
    /// Runs the same work over every entry, as many at a time as the source is willing to answer. A
    /// local disk keeps to one, so nothing changes for it; a server gets its latency hidden behind
    /// requests that overlap — and now they overlap on one thread rather than on as many.
    /// </summary>
    private static async Task SpreadAsync(
        IFileSource source,
        IReadOnlyList<FileEntry> entries,
        Func<FileEntry, Task> work,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Concurrency <= 1 || entries.Count < 2)
        {
            foreach (var entry in entries)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                await work(entry).ConfigureAwait(false);
            }

            return;
        }

        using var room = new SemaphoreSlim(source.Concurrency);
        var running = new List<Task>(entries.Count);

        foreach (var entry in entries)
        {
            if (token.IsCancellationRequested)
            {
                break;
            }

            await room.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            running.Add(Working(entry));
        }

        await Task.WhenAll(running).ConfigureAwait(false);

        async Task Working(FileEntry entry)
        {
            try
            {
                await work(entry).ConfigureAwait(false);
            }
            finally
            {
                room.Release();
            }
        }
    }

    private static async Task CopyOneAsync(
        IFileSource from,
        FileEntry source,
        IFileSource to,
        string target,
        Outcome outcome,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(from);

        if (token.IsCancellationRequested)
        {
            return;
        }

        try
        {
            outcome.Reached(source.Name);

            if (!source.IsFolder)
            {
                await TransferAsync(from, source.Path, to, target, outcome, token).ConfigureAwait(false);

                return;
            }

            await to.CreateFolderAsync(target, token).ConfigureAwait(false);
            outcome.CountedFolder();

            await SpreadAsync(
                    from.Concurrency <= to.Concurrency ? to : from,
                    await ChildrenAsync(from, source, token).ConfigureAwait(false),
                    child => CopyOneAsync(from, child, to, to.Combine(target, child.Name), outcome, token),
                    token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (IsExpected(error))
        {
            outcome.Failing(source.Name, error.Message);
        }
    }

    private static async Task MoveOneAsync(
        IFileSource from,
        FileEntry source,
        IFileSource to,
        string target,
        Outcome outcome,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(source);

        if (!ReferenceEquals(from, to) || !from.SameVolume(source.Path, target))
        {
            var copied = new Outcome();

            await CopyOneAsync(from, source, to, target, copied, token).ConfigureAwait(false);
            outcome.Absorb(copied);

            if (!copied.Failed && !token.IsCancellationRequested)
            {
                await DeleteOneAsync(from, source, new(), token).ConfigureAwait(false);
            }

            return;
        }

        try
        {
            outcome.Reached(source.Name);
            await from.MoveAsync(source.Path, target, token).ConfigureAwait(false);

            if (source.IsFolder)
            {
                outcome.CountedFolder();
            }
            else
            {
                outcome.Counted(source.Size);
            }
        }
        catch (Exception error) when (IsExpected(error))
        {
            outcome.Failing(source.Name, error.Message);
        }
    }

    /// <summary>
    /// Moves the bytes of one file.
    ///
    /// An end that can move a whole file itself is asked to, because it can keep several requests in
    /// flight where a stream can only send one and wait. The destination is asked first: writing is the
    /// narrower of the two over SFTP, where a server will take a third of what it will send.
    ///
    /// With neither end able to, the bytes go a block at a time. Reading the whole thing in one call
    /// would be shorter to write and would take the stop key with it: the block is where the work looks
    /// up. Counting per block is the other half of that — a bar that only moves as each file finishes
    /// says nothing at all while a large one is going over, which is why the pipelined paths are counted
    /// on the stream at the other end rather than left silent.
    /// </summary>
    /// <param name="from">Where the bytes are.</param>
    /// <param name="source">The file.</param>
    /// <param name="to">Where they are going.</param>
    /// <param name="target">The name they are going under.</param>
    /// <param name="outcome">Told how much moved, as it moves.</param>
    /// <param name="token">Stops it between blocks.</param>
    /// <returns>A task that finishes when the file has been written or the work called off.</returns>
    private static async Task TransferAsync(
        IFileSource from,
        string source,
        IFileSource to,
        string target,
        Outcome outcome,
        CancellationToken token)
    {
        try
        {
            switch (to, from)
            {
                case (IMovesWholeFiles sink, _):
                    await using (var reading = await from.OpenReadAsync(source, token).ConfigureAwait(false))
                    {
                        await sink.SendAsync(new CountedStream(reading, outcome.Moved), target, token)
                            .ConfigureAwait(false);
                    }

                    break;

                case (_, IMovesWholeFiles spring):
                    await using (var writing = await to.CreateAsync(target, token).ConfigureAwait(false))
                    {
                        await spring.FetchAsync(source, new CountedStream(writing, outcome.Moved), token)
                            .ConfigureAwait(false);
                    }

                    break;

                default:
                    await BlocksAsync(from, source, to, target, outcome, token).ConfigureAwait(false);

                    break;
            }

            outcome.Counted(0);
        }
        catch (OperationCanceledException)
        {
            await Abandon(to, target).ConfigureAwait(false);

            throw;
        }
    }

    /// <summary>
    /// The bytes of one file, a block at a time, for the two ends that can do no better between them.
    /// </summary>
    /// <param name="from">Where the bytes are.</param>
    /// <param name="source">The file.</param>
    /// <param name="to">Where they are going.</param>
    /// <param name="target">The name they are going under.</param>
    /// <param name="outcome">Told how much moved, as it moves.</param>
    /// <param name="token">Stops it between blocks.</param>
    /// <returns>A task that finishes when the file has been written.</returns>
    private static async Task BlocksAsync(
        IFileSource from,
        string source,
        IFileSource to,
        string target,
        Outcome outcome,
        CancellationToken token)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(Block);

        try
        {
            await using var reading = await from.OpenReadAsync(source, token).ConfigureAwait(false);
            await using var writing = await to.CreateAsync(target, token).ConfigureAwait(false);

            while (true)
            {
                var read = await reading.ReadAsync(buffer.AsMemory(0, Block), token).ConfigureAwait(false);

                if (read == 0)
                {
                    break;
                }

                await writing.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                outcome.Moved(read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Throws away what was written of a file that was stopped part way. Left behind, it is a file of
    /// the right name and the wrong length — the one shape of failure nobody notices until later.
    /// </summary>
    /// <param name="to">Where it was being written.</param>
    /// <param name="target">The half of a file.</param>
    /// <returns>A task that finishes when it is gone, or when it could not be.</returns>
    private static async Task Abandon(IFileSource to, string target)
    {
        try
        {
            await to.DeleteAsync(
                    new(to.NameOf(target), target, false, false, 0, DateTime.Now, false, false),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception error) when (IsExpected(error)) { }
    }

    private static bool IsExpected(Exception error) =>
        error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException
            or NotSupportedException;
}
