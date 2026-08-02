using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Arlecchino.Commander.Files;
using Arlecchino.Commander.Model;
using Xunit;

namespace Arlecchino.Commander.Tests;

/// <summary>
/// Copying, moving and deleting, against a folder of its own. This is the code that can lose somebody's
/// work, so what is asserted is not only that the destination appeared but that the source is where it
/// should be afterwards — gone for a move, still there for a copy, and still there when it failed.
/// </summary>
public sealed class FileTasksTests : IDisposable
{
    private readonly string _root;
    private readonly LocalSource _source = new();

    public FileTasksTests()
    {
        _root = Directory.CreateTempSubdirectory("commander-tasks").FullName;
    }

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
    public void CopyingLeavesBothCopies()
    {
        var from = Folder("from");
        var to = Folder("to");
        var file = File(from, "notes.txt", "what was written");
        var outcome = new Outcome();

        FileTasks.Copy(_source, Entries(file), _source, to, outcome, CancellationToken.None);

        Assert.False(outcome.Failed);
        Assert.Equal("what was written", System.IO.File.ReadAllText(Path.Combine(to, "notes.txt")));
        Assert.True(System.IO.File.Exists(file));
    }

    [Fact]
    public void MovingLeavesOnlyTheOne()
    {
        var from = Folder("from");
        var to = Folder("to");
        var file = File(from, "notes.txt", "what was written");
        var outcome = new Outcome();

        FileTasks.Move(_source, Entries(file), _source, to, outcome, CancellationToken.None);

        Assert.False(outcome.Failed);
        Assert.Equal("what was written", System.IO.File.ReadAllText(Path.Combine(to, "notes.txt")));
        Assert.False(System.IO.File.Exists(file));
    }

    [Fact]
    public void AFolderIsCopiedWithEverythingUnderIt()
    {
        var from = Folder("from");
        var tree = Directory.CreateDirectory(Path.Combine(from, "tree")).FullName;
        var under = Directory.CreateDirectory(Path.Combine(tree, "under")).FullName;
        var to = Folder("to");

        File(tree, "one.txt", "one");
        File(under, "two.txt", "two");

        var outcome = new Outcome();

        FileTasks.Copy(_source, Entries(tree), _source, to, outcome, CancellationToken.None);

        Assert.False(outcome.Failed);
        Assert.Equal("one", System.IO.File.ReadAllText(Path.Combine(to, "tree", "one.txt")));
        Assert.Equal("two", System.IO.File.ReadAllText(Path.Combine(to, "tree", "under", "two.txt")));
    }

    [Fact]
    public void DeletingTakesTheWholeTreeAway()
    {
        var tree = Folder("tree");
        var under = Directory.CreateDirectory(Path.Combine(tree, "under")).FullName;

        File(tree, "one.txt", "one");
        File(under, "two.txt", "two");

        var outcome = new Outcome();

        FileTasks.Delete(_source, Entries(tree), outcome, CancellationToken.None);

        Assert.False(outcome.Failed);
        Assert.False(Directory.Exists(tree));
    }

    [Fact]
    public void RenamingChangesTheNameAndNothingElse()
    {
        var folder = Folder("folder");
        var file = File(folder, "before.txt", "kept");
        var outcome = new Outcome();

        FileTasks.Rename(_source, Entry(file), Path.Combine(folder, "after.txt"), outcome);

        Assert.False(outcome.Failed);
        Assert.False(System.IO.File.Exists(file));
        Assert.Equal("kept", System.IO.File.ReadAllText(Path.Combine(folder, "after.txt")));
    }

    /// <summary>
    /// What matters when it goes wrong: the failure is reported rather than thrown, and what was being
    /// moved is still where it was.
    /// </summary>
    [Fact]
    public void AFailedMoveSaysSoAndLeavesTheSourceAlone()
    {
        var from = Folder("from");
        var file = File(from, "notes.txt", "what was written");
        var outcome = new Outcome();

        FileTasks.Move(_source, Entries(file), _source, Path.Combine(_root, "nowhere", "deeper"), outcome,
            CancellationToken.None);

        Assert.True(outcome.Failed);
        Assert.NotEmpty(outcome.Errors);
        Assert.Equal("what was written", System.IO.File.ReadAllText(file));
    }

    [Fact]
    public void MeasuringCountsWhatIsThere()
    {
        var tree = Folder("tree");
        var under = Directory.CreateDirectory(Path.Combine(tree, "under")).FullName;

        File(tree, "one.txt", "12345");
        File(under, "two.txt", "123");

        var tally = FileTasks.Measure(_source, Entries(tree), CancellationToken.None);

        Assert.Equal(2, tally.Files);
        Assert.Equal(2, tally.Folders);
        Assert.Equal(8, tally.Bytes);
        Assert.Equal(4, tally.Items);
    }

    [Fact]
    public void MakingAFolderHandsBackWhereItWasMade()
    {
        var made = FileTasks.CreateFolder(_source, _root, "fresh");

        Assert.Equal(Path.Combine(_root, "fresh"), made);
        Assert.True(Directory.Exists(Path.Combine(_root, "fresh")));
    }

    /// <summary>Folders in the way are made too, rather than the whole thing being refused.</summary>
    [Fact]
    public void MakingAFolderMakesTheOnesAboveItAsWell()
    {
        var made = FileTasks.CreateFolder(_source, Path.Combine(_root, "above"), "fresh");

        Assert.NotNull(made);
        Assert.True(Directory.Exists(Path.Combine(_root, "above", "fresh")));
    }

    [Fact]
    public void MakingAFolderThatCannotBeMadeSaysNothingWasMade()
    {
        var taken = File(Folder("busy"), "in-the-way", "");

        Assert.Null(FileTasks.CreateFolder(_source, taken, "fresh"));
    }

    [Fact]
    public void NothingIsDeletedOnceTheWorkIsCalledOff()
    {
        var from = Folder("from");

        File(from, "notes.txt", "what was written");

        using var calledOff = new CancellationTokenSource();
        calledOff.Cancel();

        var outcome = new Outcome();

        FileTasks.Delete(_source, Entries(Path.Combine(from, "notes.txt")), outcome, calledOff.Token);

        Assert.True(System.IO.File.Exists(Path.Combine(from, "notes.txt")));
    }
}
