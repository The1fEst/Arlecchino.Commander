using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Arlecchino.Commander.Files;
using Arlecchino.Commander.Model;
using Xunit;

namespace Arlecchino.Commander.Tests;

/// <summary>
/// The disk as the panels see it. This is the source everything else is measured against, so what it
/// says about a folder has to be what is actually there.
/// </summary>
public sealed class LocalSourceTests : IDisposable
{
    private readonly string _root;
    private readonly LocalSource _source = new();

    public LocalSourceTests()
    {
        _root = Directory.CreateTempSubdirectory("commander-source").FullName;
    }

    public void Dispose() => Directory.Delete(_root, true);

    private FileEntry Entry(string path) => _source
        .List(Path.GetDirectoryName(path)!, true)
        .First(entry => entry.Name == Path.GetFileName(path));

    [Fact]
    public void ItSaysWhatItIs()
    {
        Assert.False(_source.IsRemote);
        Assert.True(_source.WalksCheaply);
        Assert.Equal("local", _source.Label);
    }

    [Fact]
    public void ListingHandsBackWhatIsInTheFolder()
    {
        File.WriteAllText(Path.Combine(_root, "alpha.txt"), "one");
        Directory.CreateDirectory(Path.Combine(_root, "nested"));

        var names = _source.List(_root, false).Select(static entry => entry.Name).ToList();

        Assert.Contains("alpha.txt", names);
        Assert.Contains("nested", names);
    }

    [Fact]
    public void AFolderIsMarkedAsOne()
    {
        Directory.CreateDirectory(Path.Combine(_root, "nested"));
        File.WriteAllText(Path.Combine(_root, "alpha.txt"), "one");

        Assert.True(Entry(Path.Combine(_root, "nested")).IsFolder);
        Assert.False(Entry(Path.Combine(_root, "alpha.txt")).IsFolder);
    }

    [Fact]
    public void TheSizeIsTheSizeOnDisk()
    {
        File.WriteAllText(Path.Combine(_root, "alpha.txt"), "12345");

        Assert.Equal(5, Entry(Path.Combine(_root, "alpha.txt")).Size);
    }

    [Fact]
    public void SomethingHiddenIsShownOnlyWhenItIsAskedFor()
    {
        File.WriteAllText(Path.Combine(_root, ".hidden"), "one");

        Assert.DoesNotContain(_source.List(_root, false), static entry => entry.Name == ".hidden");
        Assert.Contains(_source.List(_root, true), static entry => entry.Name == ".hidden");
    }

    /// <summary>
    /// The source does not soften this: a folder that is not there is an exception, and turning that
    /// into something a person can read is the panel's job rather than the disk's.
    /// </summary>
    [Fact]
    public void AFolderThatIsNotThereIsAnException()
    {
        Assert.Throws<DirectoryNotFoundException>(() => _source.List(Path.Combine(_root, "never-made"), true));
    }

    [Fact]
    public void PathsAreJoinedAndTakenApartTheWayThePlatformDoes()
    {
        var joined = _source.Combine(_root, "alpha.txt");

        Assert.Equal(Path.Combine(_root, "alpha.txt"), joined);
        Assert.Equal("alpha.txt", _source.NameOf(joined));
        Assert.Equal(_root, _source.Parent(Path.Combine(_root, "nested")));
    }

    [Fact]
    public void ItKnowsWhetherAFolderIsThere()
    {
        Assert.True(_source.FolderExists(_root));
        Assert.False(_source.FolderExists(Path.Combine(_root, "never-made")));
    }

    [Fact]
    public void TwoPlacesOnTheSameDiskAreSaidToBe()
    {
        var nested = Directory.CreateDirectory(Path.Combine(_root, "nested")).FullName;

        Assert.True(_source.SameVolume(_root, nested));
    }

    [Fact]
    public void ThePermissionsAreTheOnesTheFileHas()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var path = Path.Combine(_root, "alpha.txt");
        File.WriteAllText(path, "one");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        Assert.Equal("600", _source.Mode(Entry(path)));
    }

    [Fact]
    public void ChangingThePermissionsChangesThem()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var path = Path.Combine(_root, "alpha.txt");
        File.WriteAllText(path, "one");

        Assert.True(_source.TryChangeMode(Entry(path), "640"));
        Assert.Equal("640", _source.Mode(Entry(path)));
    }

    [Fact]
    public void AMeaninglessModeIsRefusedRatherThanApplied()
    {
        var path = Path.Combine(_root, "alpha.txt");
        File.WriteAllText(path, "one");

        Assert.False(_source.TryChangeMode(Entry(path), "not a mode"));
    }

    [Fact]
    public void ALinkPointsAtWhatItWasGiven()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var target = Path.Combine(_root, "alpha.txt");
        var link = Path.Combine(_root, "pointer");

        File.WriteAllText(target, "one");

        Assert.True(_source.TryLink(link, target, false));
        Assert.Equal("one", File.ReadAllText(link));
        Assert.Equal(target, new FileInfo(link).LinkTarget);
    }
}
