using System;
using System.IO;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Watching;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Tests.Support;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// The watch away from any screen. What a panel on a server gets is a reading every so often, and the
/// source stood in for here is one nothing tells anything: a disk that says it is a server.
/// </summary>
public sealed class FolderWatchTests : IDisposable
{
    private const int TimeoutMilliseconds = 5000;
    private const int WaitMilliseconds = 700;

    private static readonly TimeSpan Briskly = TimeSpan.FromMilliseconds(50);

    private readonly string _folder = Directory.CreateTempSubdirectory("commander-watch").FullName;
    private readonly TaskCompletionSource _signal = new();
    private readonly PipingSource _source = new();

    public void Dispose()
    {
        _source.Dispose();

        Directory.Delete(_folder, true);
    }

    /// <summary>
    /// A source with nothing to watch with is read again until what comes back is not what the panel has,
    /// which is the only way an FTP or SFTP server can be followed at all.
    /// </summary>
    [Fact]
    public async Task AFolderOnASourceNothingWatchesIsReadUntilItChanges()
    {
        using var watch = Watching(Briskly);

        await FollowingAsync(watch).ConfigureAwait(true);

        await File.WriteAllTextAsync(Path.Combine(_folder, "made.txt"), "one").ConfigureAwait(true);

        Assert.Same(_signal.Task, await Task.WhenAny(_signal.Task, Task.Delay(TimeoutMilliseconds)).ConfigureAwait(true));
    }

    /// <summary>
    /// A folder nothing happened in is read and passed over. A watch that reported every reading would have
    /// the panel redrawing itself for nothing, and a server carrying the listing for it.
    /// </summary>
    [Fact]
    public async Task AFolderThatDidNotChangeIsNeverReported()
    {
        using var watch = Watching(Briskly);

        await FollowingAsync(watch).ConfigureAwait(true);
        await Task.Delay(WaitMilliseconds).ConfigureAwait(true);

        Assert.False(_signal.Task.IsCompleted);
    }

    /// <summary>The setting turned off is watched with nothing at all, server or disk.</summary>
    [Fact]
    public async Task WatchingTurnedOffFollowsNothing()
    {
        using var watch = Watching(TimeSpan.Zero);

        await FollowingAsync(watch).ConfigureAwait(true);

        await File.WriteAllTextAsync(Path.Combine(_folder, "made.txt"), "one").ConfigureAwait(true);

        await Task.Delay(WaitMilliseconds).ConfigureAwait(true);

        Assert.False(_signal.Task.IsCompleted);
    }

    /// <summary>
    /// Nothing is asked of a source that is carrying files. FTP answers over the one connection the
    /// transfer is talking on, and a reading sent into the middle of one scrambles both.
    /// </summary>
    [Fact]
    public async Task ASourceCarryingFilesIsLeftAlone()
    {
        using var watch = Watching(Briskly, carrying: true);

        await FollowingAsync(watch).ConfigureAwait(true);

        await File.WriteAllTextAsync(Path.Combine(_folder, "made.txt"), "one").ConfigureAwait(true);

        await Task.Delay(WaitMilliseconds).ConfigureAwait(true);

        Assert.False(_signal.Task.IsCompleted);
    }

    /// <summary>
    /// A folder read again by the panel itself is not news. The watch is told what that reading found, so
    /// what the panel already has never comes back to it as a change.
    /// </summary>
    [Fact]
    public async Task WhatThePanelReadItselfIsNotReportedBack()
    {
        using var watch = Watching(Briskly);

        await FollowingAsync(watch).ConfigureAwait(true);

        await File.WriteAllTextAsync(Path.Combine(_folder, "made.txt"), "one").ConfigureAwait(true);

        await FollowingAsync(watch).ConfigureAwait(true);
        await Task.Delay(WaitMilliseconds).ConfigureAwait(true);

        Assert.False(_signal.Task.IsCompleted);
    }

    /// <summary>A watch that reports by finishing the task the test waits on.</summary>
    /// <param name="interval">How often to read the folder again.</param>
    /// <param name="carrying">Whether to claim that files are being carried.</param>
    /// <returns>The watch, for the test to dispose.</returns>
    private FolderWatch Watching(TimeSpan interval, bool carrying = false) =>
        new(() => interval, () => carrying, () => _signal.TrySetResult());

    /// <summary>Reads the folder the way a panel does and hands the watch what came back.</summary>
    /// <param name="watch">The watch to arm.</param>
    /// <returns>A task that finishes once the watch is following.</returns>
    private async Task FollowingAsync(FolderWatch watch)
    {
        var lines = await Listing.ReadAsync(_source, _folder, false).ConfigureAwait(true);

        watch.Follow(_source, _folder, false, lines.Entries);
    }
}
