using System;
using System.Collections.Generic;
using System.IO;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Files.Work;
using Arlecchino.Commander.Model;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// What the command line makes of <c>%s</c> and its fellows. The escaping is the point of most of these,
/// since a marked file with an apostrophe in its name is ordinary.
/// </summary>
public sealed class PlaceholdersTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("commander-placeholders").FullName;
    private readonly LocalSource _source = new();

    public void Dispose() => Directory.Delete(_root, true);

    private string Path(string name) => System.IO.Path.Combine(_root, name);

    private FileEntry Entry(string name) => new(name, Path(name), false, false, 0, default, false, false);

    private IReadOnlyList<FileEntry> Entries(params string[] names)
    {
        var entries = new FileEntry[names.Length];

        for (var at = 0; at < names.Length; at++)
        {
            entries[at] = Entry(names[at]);
        }

        return entries;
    }

    private string Expand(string command, IReadOnlyList<FileEntry> targets, FileEntry? current = null) =>
        Placeholders.Expand(command, _source, _root, targets, current);

    [Fact]
    public void ACommandWithoutAnyOfTheWordsIsLeftAlone()
    {
        Assert.Equal("git status", Expand("git status", Entries("a.txt")));
    }

    [Fact]
    public void MarkedFilesGoInSpacedApart()
    {
        var expanded = Expand("wc -l %s", Entries("a.txt", "b.txt"));

        Assert.Equal($"wc -l {_source.Quote(Path("a.txt"))} {_source.Quote(Path("b.txt"))}", expanded);
    }

    [Fact]
    public void TheCursorGoesInOnItsOwn()
    {
        var expanded = Expand("file %f", Entries("a.txt", "b.txt"), Entry("b.txt"));

        Assert.Equal($"file {_source.Quote(Path("b.txt"))}", expanded);
    }

    [Fact]
    public void TheFolderGoesIn()
    {
        Assert.Equal($"du -sh {_source.Quote(_root)}", Expand("du -sh %d", []));
    }

    /// <summary>
    /// The reason this is not a few calls to <c>Replace</c>. Pasted in raw, the apostrophe would close
    /// the quoting the shell had opened and the rest of the name would be read as more arguments.
    /// </summary>
    [Fact]
    public void AnApostropheInANameCannotEndUpAsSyntax()
    {
        var expanded = Expand("wc -l %s", Entries("don't go.txt"));

        Assert.StartsWith("wc -l ", expanded, StringComparison.Ordinal);
        Assert.Contains("don", expanded, StringComparison.Ordinal);
        Assert.NotEqual($"wc -l {Path("don't go.txt")}", expanded);
        Assert.Equal($"wc -l {_source.Quote(Path("don't go.txt"))}", expanded);
    }

    [Fact]
    public void ADoubledPercentIsJustAPercent()
    {
        Assert.Equal("echo 50%", Expand("echo 50%%", []));
    }

    /// <summary>
    /// A command line is full of percents meant for something else: a Windows variable, a format string,
    /// a URL. What is not one of the words is left standing, percent and all.
    /// </summary>
    [Fact]
    public void APercentThatIsNotOneOfTheWordsIsLeftStanding()
    {
        Assert.Equal("echo %PATH%", Expand("echo %PATH%", []));
        Assert.Equal("printf %n", Expand("printf %n", []));
    }

    [Fact]
    public void APercentAtTheVeryEndIsLeftStanding()
    {
        Assert.Equal("echo %", Expand("echo %", []));
    }

    [Fact]
    public void NothingMarkedAndNoCursorLeavesTheWordsEmpty()
    {
        Assert.Equal("wc -l ", Expand("wc -l %s", []));
        Assert.Equal("file ", Expand("file %f", []));
    }
}
