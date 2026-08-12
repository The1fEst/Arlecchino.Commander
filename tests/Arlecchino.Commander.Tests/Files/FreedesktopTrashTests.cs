using System;
using System.IO;
using System.Linq;
using Arlecchino.Commander.Files.Trash;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// The trash the Linux desktops share, which is a file format rather than a system call and so can be
/// held to account on any machine. What is asserted is the sidecar a desktop reads to put a file back.
/// </summary>
public sealed class FreedesktopTrashTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("commander-trash").FullName;
    private readonly string _from = Directory.CreateTempSubdirectory("commander-trash-from").FullName;
    private readonly FreedesktopTrash _trash;

    public FreedesktopTrashTests() => _trash = new(Path.Combine(_root, "Trash"));

    public void Dispose()
    {
        Directory.Delete(_root, true);
        Directory.Delete(_from, true);
    }

    private string Written(string name, string text = "what was written")
    {
        var path = Path.Combine(_from, name);
        File.WriteAllText(path, text);

        return path;
    }

    private string Backing(string name) => Path.Combine(_root, "Trash", "files", name);

    private string Sidecar(string name) => Path.Combine(_root, "Trash", "info", name + ".trashinfo");

    [Fact]
    public void TheFileMovesAndASidecarIsLeftBehind()
    {
        var path = Written("notes.txt");

        Assert.True(_trash.TryPut(path));
        Assert.False(File.Exists(path));
        Assert.Equal("what was written", File.ReadAllText(Backing("notes.txt")));
        Assert.True(File.Exists(Sidecar("notes.txt")));
    }

    /// <summary>
    /// The sidecar has to say where the file came from, or nothing can put it back. The header is fixed,
    /// and the date has no zone on it.
    /// </summary>
    [Fact]
    public void TheSidecarSaysWhereTheFileCameFrom()
    {
        var path = Written("notes.txt");

        _trash.TryPut(path);

        var lines = File.ReadAllLines(Sidecar("notes.txt"));

        Assert.Equal("[Trash Info]", lines[0]);
        Assert.Contains(lines, line => line == $"Path={path}");
        Assert.Contains(lines, line => line.StartsWith("DeletionDate=", StringComparison.Ordinal));
    }

    /// <summary>
    /// A space in a name is ordinary and a percent in the recorded path is not allowed to be ambiguous,
    /// so the path is URL-encoded going in.
    /// </summary>
    [Fact]
    public void AnAwkwardNameIsEscapedInTheSidecar()
    {
        var path = Written("a b&c%d.txt");

        Assert.True(_trash.TryPut(path));

        var recorded = File.ReadAllLines(Sidecar("a b&c%d.txt"))
            .Single(line => line.StartsWith("Path=", StringComparison.Ordinal));

        Assert.Contains("%20", recorded, StringComparison.Ordinal);
        Assert.Contains("%26", recorded, StringComparison.Ordinal);
        Assert.Contains("%25", recorded, StringComparison.Ordinal);
        Assert.DoesNotContain(" ", recorded, StringComparison.Ordinal);
    }

    /// <summary>
    /// Deleting two files of the same name from different folders is ordinary, and the second must not
    /// land on top of the first.
    /// </summary>
    [Fact]
    public void ASecondFileOfTheSameNameDoesNotLandOnTheFirst()
    {
        _trash.TryPut(Written("notes.txt", "the first"));

        var second = Directory.CreateDirectory(Path.Combine(_from, "elsewhere")).FullName;
        var path = Path.Combine(second, "notes.txt");
        File.WriteAllText(path, "the second");

        Assert.True(_trash.TryPut(path));
        Assert.Equal("the first", File.ReadAllText(Backing("notes.txt")));
        Assert.Equal("the second", File.ReadAllText(Backing("notes_1.txt")));
        Assert.True(File.Exists(Sidecar("notes_1.txt")));
    }

    [Fact]
    public void AFolderGoesInWholeWithWhatIsInsideIt()
    {
        var tree = Directory.CreateDirectory(Path.Combine(_from, "tree")).FullName;
        File.WriteAllText(Path.Combine(tree, "deep.txt"), "deep");

        Assert.True(_trash.TryPut(tree));
        Assert.False(Directory.Exists(tree));
        Assert.Equal("deep", File.ReadAllText(Path.Combine(Backing("tree"), "deep.txt")));
    }

    /// <summary>
    /// A claim left behind for a file that never arrived would show in the desktop's trash as an entry
    /// that can be neither restored nor got rid of.
    /// </summary>
    [Fact]
    public void NothingIsClaimedWhenThereIsNothingToMove()
    {
        Assert.False(_trash.TryPut(Path.Combine(_from, "never-was.txt")));
        Assert.False(File.Exists(Sidecar("never-was.txt")));
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "Trash", "info")));
    }
}
