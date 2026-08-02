using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arlecchino.Commander.Files;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Views;
using Xunit;

namespace Arlecchino.Commander.Tests;

/// <summary>
/// The work behind F5, F6 and F8, run the way the application runs it: off the drawing thread, with a
/// bar and a revision that tell the panels something changed.
/// </summary>
public sealed class OperationsTests : IDisposable
{
    private readonly ScreenApp _app = new(ViewKind.Commander);
    private readonly LocalSource _source = new();

    public void Dispose() => _app.Dispose();

    private string Folder(string name) =>
        Directory.CreateDirectory(Path.Combine(_app.Folder, name)).FullName;

    private static FileEntry Entry(string path) => new(
        Path.GetFileName(path),
        path,
        Directory.Exists(path),
        false,
        Directory.Exists(path) ? 0 : new FileInfo(path).Length,
        File.GetLastWriteTime(path),
        false,
        false);

    private static IReadOnlyList<FileEntry> Entries(params string[] paths) => [.. paths.Select(Entry)];

    private bool Settled() => _app.Until(() => !_app.Operations.IsBusy);

    [Fact]
    public void NothingIsBusyBeforeAnythingIsAskedFor()
    {
        Assert.False(_app.Operations.IsBusy);
        Assert.Equal("", _app.Operations.Progress());
    }

    [Fact]
    public void CopyingPutsTheFileWhereItWasAskedFor()
    {
        var from = Folder("from");
        var to = Folder("to");

        File.WriteAllText(Path.Combine(from, "notes.txt"), "kept");

        _app.Operations.Copy(_source, Entries(Path.Combine(from, "notes.txt")), _source, to);

        Assert.True(Settled());
        Assert.Equal("kept", File.ReadAllText(Path.Combine(to, "notes.txt")));
    }

    [Fact]
    public void MovingTakesItOutOfWhereItWas()
    {
        var from = Folder("from");
        var to = Folder("to");
        var file = Path.Combine(from, "notes.txt");

        File.WriteAllText(file, "kept");

        _app.Operations.Move(_source, Entries(file), _source, to);

        Assert.True(Settled());
        Assert.False(File.Exists(file));
        Assert.True(File.Exists(Path.Combine(to, "notes.txt")));
    }

    [Fact]
    public void DeletingTakesItAway()
    {
        var folder = Folder("folder");
        var file = Path.Combine(folder, "notes.txt");

        File.WriteAllText(file, "kept");

        _app.Operations.Delete(_source, Entries(file));

        Assert.True(Settled());
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void RenamingChangesTheName()
    {
        var folder = Folder("folder");
        var file = Path.Combine(folder, "before.txt");

        File.WriteAllText(file, "kept");

        _app.Operations.Rename(_source, Entry(file), Path.Combine(folder, "after.txt"));

        Assert.True(Settled());
        Assert.True(File.Exists(Path.Combine(folder, "after.txt")));
    }

    /// <summary>
    /// The panels redraw off a revision rather than by watching the disk, so work that changed
    /// something has to say so or the change stays invisible until something else asks for a frame.
    /// </summary>
    [Fact]
    public void WorkThatChangedSomethingSaysSo()
    {
        var from = Folder("from");
        var to = Folder("to");

        File.WriteAllText(Path.Combine(from, "notes.txt"), "kept");

        var before = _app.Operations.Revision.Value;

        _app.Operations.Copy(_source, Entries(Path.Combine(from, "notes.txt")), _source, to);

        Assert.True(Settled());
        Assert.True(_app.Operations.Revision.Value > before);
    }

    [Fact]
    public void AFailureIsSaidRatherThanThrown()
    {
        var from = Folder("from");

        File.WriteAllText(Path.Combine(from, "notes.txt"), "kept");

        _app.Operations.Copy(
            _source,
            Entries(Path.Combine(from, "notes.txt")),
            _source,
            Path.Combine(_app.Folder, "in-the-way", "deeper"));

        Assert.True(Settled());
        Assert.True(File.Exists(Path.Combine(from, "notes.txt")));
    }
}
