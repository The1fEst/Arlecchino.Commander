using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Files.Work;
using Arlecchino.Commander.Model;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// Copying, moving and deleting, against a folder of its own. This is the code that can lose somebody's
/// work, so what is asserted is not only that the destination appeared but that the source is where it
/// should be afterwards — gone for a move, still there for a copy, and still there when it failed.
/// </summary>
public sealed class FileTasksTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("commander-tasks").FullName;
    private readonly LocalSource _source = new();

    public void Dispose() => Directory.Delete(_root, true);

    private string Folder(string name) => Directory.CreateDirectory(Path.Combine(_root, name)).FullName;

    private static string File(string folder, string name, string text)
    {
        var path = Path.Combine(folder, name);
        System.IO.File.WriteAllText(path, text);

        return path;
    }

    private static FileEntry Entry(string path) => new(
        Path.GetFileName(path),
        path,
        Directory.Exists(path),
        false,
        Directory.Exists(path) ? 0 : new FileInfo(path).Length,
        System.IO.File.GetLastWriteTime(path),
        false,
        false);

    private static IReadOnlyList<FileEntry> Entries(params string[] paths) => [.. paths.Select(Entry)];

    [Fact]
    public async Task CopyingLeavesBothCopies()
    {
        var from = Folder("from");
        var to = Folder("to");
        var file = File(from, "notes.txt", "what was written");
        var outcome = new Outcome();

        await FileTasks.CopyAsync(_source, Entries(file), _source, to, outcome, CancellationToken.None);

        Assert.False(outcome.Failed);
        Assert.Equal("what was written", await System.IO.File.ReadAllTextAsync(Path.Combine(to, "notes.txt")));
        Assert.True(System.IO.File.Exists(file));
    }

    [Fact]
    public async Task MovingLeavesOnlyTheOne()
    {
        var from = Folder("from");
        var to = Folder("to");
        var file = File(from, "notes.txt", "what was written");
        var outcome = new Outcome();

        await FileTasks.MoveAsync(_source, Entries(file), _source, to, outcome, CancellationToken.None);

        Assert.False(outcome.Failed);
        Assert.Equal("what was written", await System.IO.File.ReadAllTextAsync(Path.Combine(to, "notes.txt")));
        Assert.False(System.IO.File.Exists(file));
    }

    [Fact]
    public async Task AFolderIsCopiedWithEverythingUnderIt()
    {
        var from = Folder("from");
        var tree = Directory.CreateDirectory(Path.Combine(from, "tree")).FullName;
        var under = Directory.CreateDirectory(Path.Combine(tree, "under")).FullName;
        var to = Folder("to");

        File(tree, "one.txt", "one");
        File(under, "two.txt", "two");

        var outcome = new Outcome();

        await FileTasks.CopyAsync(_source, Entries(tree), _source, to, outcome, CancellationToken.None);

        Assert.False(outcome.Failed);
        Assert.Equal("one", await System.IO.File.ReadAllTextAsync(Path.Combine(to, "tree", "one.txt")));
        Assert.Equal("two", await System.IO.File.ReadAllTextAsync(Path.Combine(to, "tree", "under", "two.txt")));
    }

    [Fact]
    public async Task DeletingTakesTheWholeTreeAway()
    {
        var tree = Folder("tree");
        var under = Directory.CreateDirectory(Path.Combine(tree, "under")).FullName;

        File(tree, "one.txt", "one");
        File(under, "two.txt", "two");

        var outcome = new Outcome();

        await FileTasks.DeleteAsync(_source, Entries(tree), outcome, CancellationToken.None);

        Assert.False(outcome.Failed);
        Assert.False(Directory.Exists(tree));
    }

    [Fact]
    public async Task RenamingChangesTheNameAndNothingElse()
    {
        var folder = Folder("folder");
        var file = File(folder, "before.txt", "kept");
        var outcome = new Outcome();

        await FileTasks.RenameAsync(_source, Entry(file), Path.Combine(folder, "after.txt"), outcome);

        Assert.False(outcome.Failed);
        Assert.False(System.IO.File.Exists(file));
        Assert.Equal("kept", await System.IO.File.ReadAllTextAsync(Path.Combine(folder, "after.txt")));
    }

    /// <summary>
    /// What matters when it goes wrong: the failure is reported rather than thrown, and what was being
    /// moved is still where it was.
    /// </summary>
    [Fact]
    public async Task AFailedMoveSaysSoAndLeavesTheSourceAlone()
    {
        var from = Folder("from");
        var file = File(from, "notes.txt", "what was written");
        var outcome = new Outcome();

        await FileTasks.MoveAsync(_source, Entries(file), _source, Path.Combine(_root, "nowhere", "deeper"), outcome,
            CancellationToken.None);

        Assert.True(outcome.Failed);
        Assert.NotEmpty(outcome.Errors);
        Assert.Equal("what was written", await System.IO.File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task MeasuringCountsWhatIsThere()
    {
        var tree = Folder("tree");
        var under = Directory.CreateDirectory(Path.Combine(tree, "under")).FullName;

        File(tree, "one.txt", "12345");
        File(under, "two.txt", "123");

        var tally = await FileTasks.MeasureAsync(_source, Entries(tree), CancellationToken.None);

        Assert.Equal(2, tally.Files);
        Assert.Equal(2, tally.Folders);
        Assert.Equal(8, tally.Bytes);
        Assert.Equal(4, tally.Items);
    }

    [Fact]
    public async Task MakingAFolderHandsBackWhereItWasMade()
    {
        var made = await FileTasks.CreateFolderAsync(_source, _root, "fresh", CancellationToken.None);

        Assert.Equal(Path.Combine(_root, "fresh"), made);
        Assert.True(Directory.Exists(Path.Combine(_root, "fresh")));
    }

    /// <summary>Folders in the way are made too, rather than the whole thing being refused.</summary>
    [Fact]
    public async Task MakingAFolderMakesTheOnesAboveItAsWell()
    {
        var made = await FileTasks.CreateFolderAsync(_source, Path.Combine(_root, "above"), "fresh",
            CancellationToken.None);

        Assert.NotNull(made);
        Assert.True(Directory.Exists(Path.Combine(_root, "above", "fresh")));
    }

    [Fact]
    public async Task MakingAFolderThatCannotBeMadeSaysNothingWasMade()
    {
        var taken = File(Folder("busy"), "in-the-way", "");

        Assert.Null(await FileTasks.CreateFolderAsync(_source, taken, "fresh", CancellationToken.None));
    }

    [Fact]
    public async Task NothingIsDeletedOnceTheWorkIsCalledOff()
    {
        var from = Folder("from");

        File(from, "notes.txt", "what was written");

        using var calledOff = new CancellationTokenSource();
        await calledOff.CancelAsync();

        var outcome = new Outcome();

        await FileTasks.DeleteAsync(_source, Entries(Path.Combine(from, "notes.txt")), outcome, calledOff.Token);

        Assert.True(System.IO.File.Exists(Path.Combine(from, "notes.txt")));
    }
}
