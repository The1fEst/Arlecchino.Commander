using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// The disk as the panels see it. This is the source everything else is measured against, so what it
/// says about a folder has to be what is actually there.
/// </summary>
public sealed class LocalSourceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("commander-source").FullName;
    private readonly LocalSource _source = new();

    public void Dispose() => Directory.Delete(_root, true);

    private async Task<FileEntry> Entry(string path) =>
        (await _source.ListAsync(Path.GetDirectoryName(path)!, true, CancellationToken.None))
        .First(entry => entry.Name == Path.GetFileName(path));

    private Task<IReadOnlyList<FileEntry>> Listed(string folder, bool showHidden) =>
        _source.ListAsync(folder, showHidden, CancellationToken.None);

    [Fact]
    public void ItSaysWhatItIs()
    {
        Assert.False(_source.IsRemote);
        Assert.True(_source.WalksCheaply);
        Assert.Equal("local", _source.Label);
    }

    [Fact]
    public async Task ListingHandsBackWhatIsInTheFolder()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "alpha.txt"), "one");
        Directory.CreateDirectory(Path.Combine(_root, "nested"));

        var names = (await Listed(_root, false)).Select(static entry => entry.Name).ToList();

        Assert.Contains("alpha.txt", names);
        Assert.Contains("nested", names);
    }

    [Fact]
    public async Task AFolderIsMarkedAsOne()
    {
        Directory.CreateDirectory(Path.Combine(_root, "nested"));
        await File.WriteAllTextAsync(Path.Combine(_root, "alpha.txt"), "one");

        Assert.True((await Entry(Path.Combine(_root, "nested"))).IsFolder);
        Assert.False((await Entry(Path.Combine(_root, "alpha.txt"))).IsFolder);
    }

    [Fact]
    public async Task TheSizeIsTheSizeOnDisk()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "alpha.txt"), "12345");

        Assert.Equal(5, (await Entry(Path.Combine(_root, "alpha.txt"))).Size);
    }

    [Fact]
    public async Task SomethingHiddenIsShownOnlyWhenItIsAskedFor()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, ".hidden"), "one");

        Assert.DoesNotContain(await Listed(_root, false), static entry => entry.Name == ".hidden");
        Assert.Contains(await Listed(_root, true), static entry => entry.Name == ".hidden");
    }

    /// <summary>
    /// The source does not soften this: a folder that is not there is an exception, and turning that
    /// into something a person can read is the panel's job rather than the disk's.
    /// </summary>
    [Fact]
    public async Task AFolderThatIsNotThereIsAnException()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => Listed(Path.Combine(_root, "never-made"), true));
    }

    [Fact]
    public void PathsAreJoinedAndTakenApartTheWayThePlatformDoes()
    {
        var path = _source.Combine(_root, "alpha.txt");

        Assert.Equal(Path.Combine(_root, "alpha.txt"), path);
        Assert.Equal("alpha.txt", _source.NameOf(path));
        Assert.Equal(_root, _source.Parent(Path.Combine(_root, "nested")));
    }

    [Fact]
    public async Task ItKnowsWhetherAFolderIsThere()
    {
        Assert.True(await _source.FolderExistsAsync(_root, CancellationToken.None));
        Assert.False(await _source.FolderExistsAsync(Path.Combine(_root, "never-made"), CancellationToken.None));
    }

    [Fact]
    public void TwoPlacesOnTheSameDiskAreSaidToBe()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "nested")).FullName;

        Assert.True(_source.SameVolume(_root, folder));
    }

    [Fact]
    public async Task ThePermissionsAreTheOnesTheFileHas()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var path = Path.Combine(_root, "alpha.txt");
        await File.WriteAllTextAsync(path, "one");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        Assert.Equal("600", await _source.ModeAsync(await Entry(path), CancellationToken.None));
    }

    [Fact]
    public async Task ChangingThePermissionsChangesThem()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var path = Path.Combine(_root, "alpha.txt");
        await File.WriteAllTextAsync(path, "one");

        Assert.True(await _source.TryChangeModeAsync(await Entry(path), "640", CancellationToken.None));
        Assert.Equal("640", await _source.ModeAsync(await Entry(path), CancellationToken.None));
    }

    [Fact]
    public async Task AMeaninglessModeIsRefusedRatherThanApplied()
    {
        var path = Path.Combine(_root, "alpha.txt");
        await File.WriteAllTextAsync(path, "one");

        Assert.False(await _source.TryChangeModeAsync(await Entry(path), "not a mode", CancellationToken.None));
    }

    [Fact]
    public async Task ALinkPointsAtWhatItWasGiven()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var target = Path.Combine(_root, "alpha.txt");
        var link = Path.Combine(_root, "pointer");

        await File.WriteAllTextAsync(target, "one");

        Assert.True(await _source.TryLinkAsync(link, target, false, CancellationToken.None));
        Assert.Equal("one", await File.ReadAllTextAsync(link));
        Assert.Equal(target, new FileInfo(link).LinkTarget);
    }
}
