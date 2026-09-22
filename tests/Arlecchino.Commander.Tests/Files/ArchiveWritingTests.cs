using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Archives;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Files.Work;
using Arlecchino.Commander.Model;
using Xunit;
using Xunit.Abstractions;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// Writing into an archive, which is the program rewriting it rather than a stream going in. Every
/// assertion here reads the archive back, since what matters is what is in it at the end.
/// </summary>
public sealed class ArchiveWritingTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("commander-writing").FullName;

    private readonly ITestOutputHelper _output;

    public ArchiveWritingTests(ITestOutputHelper output) => _output = output;

    public void Dispose() => Directory.Delete(_folder, true);

    private bool PassedOver()
    {
        if (SevenZip.Works)
        {
            return false;
        }

        _output.WriteLine("Passed over: this machine has no 7-Zip.");

        return true;
    }

    /// <summary>An archive holding one file in a folder, for the writing to be done to.</summary>
    /// <param name="name">What to call it.</param>
    /// <returns>The archive, as a path on this disk.</returns>
    private string Archive(string name)
    {
        var loose = Directory.CreateDirectory(Path.Combine(_folder, "loose", "tree")).FullName;

        File.WriteAllText(Path.Combine(loose, "kept.txt"), "kept");
        File.WriteAllText(Path.Combine(loose, "gone.txt"), "gone");

        var path = Path.Combine(_folder, name);

        Ran(SevenZip.Starting("a", path, loose));
        Directory.Delete(Path.Combine(_folder, "loose"), true);

        return path;
    }

    private static void Ran(System.Diagnostics.ProcessStartInfo start)
    {
        using var running = System.Diagnostics.Process.Start(start)!;

        running.StandardOutput.ReadToEnd();
        running.StandardError.ReadToEnd();
        running.WaitForExit();

        Assert.Equal(0, running.ExitCode);
    }

    private static async Task<string[]> Inside(ArchiveSource source, string folder)
    {
        var rows = await source.ListAsync(folder, showHidden: true, CancellationToken.None);

        return [.. rows.Where(static row => !row.IsParent).Select(static row => row.Name).Order()];
    }

    /// <summary>
    /// A file on the disk goes into the archive, into the folder the panel was looking at. What was
    /// already there is left where it was.
    /// </summary>
    [Fact]
    public async Task AFileIsPutIntoTheFolderThePanelIsLookingAt()
    {
        if (PassedOver())
        {
            return;
        }

        var path = Archive("bundle.7z");
        var beside = Path.Combine(_folder, "added.txt");

        await File.WriteAllTextAsync(beside, "added");

        using var source = new ArchiveSource(path, SevenZipArchives.Instance);
        using var disk = new LocalSource();
        var outcome = new Outcome();
        var entries = await disk.ListAsync(_folder, showHidden: false, CancellationToken.None);
        var file = entries.Single(static entry => entry.Name == "added.txt");

        await FileTasks.CopyAsync(disk, [file], source, "/tree", outcome, CancellationToken.None);

        Assert.False(outcome.Failed);
        Assert.Equal(["added.txt", "gone.txt", "kept.txt"], await Inside(source, "/tree"));
    }

    /// <summary>The panel reads the archive again at the end, so the table of contents has to be too.</summary>
    [Fact]
    public async Task WhatIsTakenOutIsGoneWhenTheArchiveIsListedAgain()
    {
        if (PassedOver())
        {
            return;
        }

        var path = Archive("bundle.7z");

        using var source = new ArchiveSource(path, SevenZipArchives.Instance);
        var outcome = new Outcome();
        var rows = await source.ListAsync("/tree", showHidden: true, CancellationToken.None);
        var goneFile = rows.Single(static row => row.Name == "gone.txt");

        await FileTasks.DeleteAsync(source, [goneFile], toTrash: false, outcome, CancellationToken.None);

        Assert.False(outcome.Failed);
        Assert.Equal(["kept.txt"], await Inside(source, "/tree"));
    }

    [Fact]
    public async Task AnEntryIsGivenAnotherName()
    {
        if (PassedOver())
        {
            return;
        }

        var path = Archive("bundle.7z");

        using var source = new ArchiveSource(path, SevenZipArchives.Instance);
        var outcome = new Outcome();
        var rows = await source.ListAsync("/tree", showHidden: true, CancellationToken.None);
        var keptFile = rows.Single(static row => row.Name == "kept.txt");

        await FileTasks.RenameAsync(source, keptFile, "/tree/renamed.txt", outcome);

        Assert.False(outcome.Failed);
        Assert.Equal(["gone.txt", "renamed.txt"], await Inside(source, "/tree"));
    }

    /// <summary>
    /// The formats the program will not rewrite are known before anything is tried. A two-layer archive
    /// reads perfectly well and refuses every write, which is worth knowing before the dialog opens.
    /// </summary>
    /// <param name="name">The archive.</param>
    /// <param name="writable">Whether writing into it is offered.</param>
    [Theory]
    [InlineData("bundle.7z", true)]
    [InlineData("bundle.zip", true)]
    [InlineData("source.tar", true)]
    [InlineData("source.tar.gz", false)]
    [InlineData("source.tar.xz", false)]
    [InlineData("bundle.rar", false)]
    [InlineData("windows.iso", false)]
    public void OnlyTheFormatsTheProgramRewritesAreWritable(string name, bool writable)
    {
        Assert.Equal(writable, Archives.Writable(name));
    }

    /// <summary>
    /// What the program refused is reported with what it said about it. Failing quietly would leave
    /// the panel looking unchanged with no reason given for it.
    /// </summary>
    [Fact]
    public async Task WorkTheProgramRefusedIsReportedWithItsReason()
    {
        if (PassedOver())
        {
            return;
        }

        var path = Archive("bundle.7z");

        using var source = new ArchiveSource(path, SevenZipArchives.Instance);
        using var disk = new LocalSource();
        var outcome = new Outcome();
        var missing = new FileEntry(
            "nowhere.txt",
            Path.Combine(_folder, "nowhere.txt"),
            false,
            false,
            0,
            default,
            false,
            false,
            false);

        await source.PackAsync(disk, [missing], "/tree", outcome, CancellationToken.None);

        Assert.True(outcome.Failed);
        Assert.NotEmpty(outcome.Errors);
        Assert.Equal(["gone.txt", "kept.txt"], await Inside(source, "/tree"));
    }
}
