using System;
using System.Collections.Generic;
using System.Linq;
using Arlecchino.Commander.Files.Archives;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// The flat list an archive holds, read as folders. Every format writes whole paths and most of them
/// write no folder of their own, so the tree is made rather than read.
/// </summary>
public sealed class ArchiveTreeTests
{
    private static readonly DateTime WriteTime = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Unspecified);

    private static ArchiveTree Holding(params string[] paths)
    {
        var entries = new List<ArchiveEntry>(paths.Length);

        foreach (var path in paths)
        {
            entries.Add(new(path, path.EndsWith('/') ? 0 : 11, WriteTime, path.EndsWith('/')));
        }

        return new(entries);
    }

    private static string[] Names(ArchiveTree tree, string folder) =>
        [.. tree.List(folder, showHidden: true).Where(static row => !row.IsParent).Select(static row => row.Name)];

    /// <summary>
    /// A folder no entry was written for is still a folder. Every format is free to hold one deep path
    /// and nothing else, and most of them do.
    /// </summary>
    [Fact]
    public void AFolderTheArchiveNeverWroteIsStillWalkedInto()
    {
        var tree = Holding("src/deep/main.c");

        Assert.Equal(["src"], Names(tree, "/"));
        Assert.Equal(["deep"], Names(tree, "/src"));
        Assert.Equal(["main.c"], Names(tree, "/src/deep"));
        Assert.True(tree.Has("/src/deep"));
    }

    /// <summary>What is read off a file entry is the size and the date it was written.</summary>
    [Fact]
    public void AFileCarriesItsSizeAndItsDate()
    {
        var file = Holding("notes.txt").List("/", showHidden: true).Single(static row => !row.IsParent);

        Assert.False(file.IsFolder);
        Assert.Equal("/notes.txt", file.Path);
        Assert.Equal(11, file.Size);
        Assert.Equal(WriteTime, file.Modified);
    }

    /// <summary>
    /// A folder written for its own sake and a folder made on the way to a file are one folder. An
    /// archive that holds both would otherwise show the name twice.
    /// </summary>
    [Fact]
    public void AFolderWrittenTwiceOverIsOneRow()
    {
        Assert.Equal(["src"], Names(Holding("src/", "src/main.c"), "/"));
    }

    /// <summary>
    /// The shapes the formats differ on are all the same path: a leading slash, a leading dot, and
    /// backslashes from an archive made on Windows.
    /// </summary>
    /// <param name="path">The path as the archive wrote it.</param>
    [Theory]
    [InlineData("src/main.c")]
    [InlineData("/src/main.c")]
    [InlineData("./src/main.c")]
    [InlineData("src\\main.c")]
    public void ThePathIsReadHoweverTheArchiveWroteIt(string path)
    {
        Assert.Equal(["main.c"], Names(Holding(path), "/src"));
    }

    /// <summary>The way out is the first row of every folder, as it is on a disk.</summary>
    [Fact]
    public void TheWayOutLeadsEveryFolder()
    {
        var tree = Holding("src/main.c");

        Assert.Equal("/", tree.List("/src", showHidden: true)[0].Path);
    }

    /// <summary>
    /// At the root the way out names nowhere, since what is above the root of an archive is the folder
    /// the archive sits in and no path inside it says so.
    /// </summary>
    [Fact]
    public void AtTheRootTheWayOutNamesNowhere()
    {
        var parent = Holding("src/main.c").List("/", showHidden: true)[0];

        Assert.True(parent.IsParent);
        Assert.Equal("", parent.Path);
    }

    [Fact]
    public void TheHiddenNamesAreKeptBackUnlessTheyAreAskedFor()
    {
        var tree = Holding(".env", "notes.txt");

        Assert.Equal(["notes.txt"],
        [
            .. tree.List("/", showHidden: false)
                .Where(static row => !row.IsParent)
                .Select(static row => row.Name)
        ]);
        Assert.Equal([".env", "notes.txt"], Names(tree, "/"));
    }

    /// <summary>A folder that is not in the archive holds nothing, rather than throwing at the panel.</summary>
    [Fact]
    public void AFolderThatIsNotThereHoldsNothing()
    {
        var tree = Holding("src/main.c");

        Assert.False(tree.Has("/nowhere"));
        Assert.Empty(tree.List("/nowhere", showHidden: true));
    }
}
