using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Arlecchino.Commander.Files.Archives;
using Arlecchino.Commander.Tests.Support;
using Arlecchino.Commander.Views;
using Xunit;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
/// An archive entered from the panel and left again. What is asserted is where the panel ends up, since
/// entering one is a connection made and leaving it is the connection given up.
/// </summary>
public sealed class CommanderArchiveTests : IDisposable
{
    private readonly ScreenApp _app = new(ViewKind.Commander);

    public CommanderArchiveTests()
    {
        var path = Path.Combine(_app.Folder, "bundle.zip");

        using (var file = File.Create(path))
        using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
        {
            foreach (var inside in new[] { "readme.md", "src/main.c" })
            {
                using var writing = new StreamWriter(zip.CreateEntry(inside).Open(), new UTF8Encoding(false));

                writing.Write("what is in it");
            }
        }

        _app.Sessions.Start(_app.Folder, _app.Folder);
        _app.Settled();
    }

    public void Dispose() => _app.Dispose();

    private void OnTheArchive()
    {
        _app.Press(ConsoleKey.DownArrow);
        _app.Frame();
    }

    /// <summary>
    /// Enter on an archive walks into it rather than handing it to the desktop. What the application
    /// can open itself it opens itself, exactly as it does with a folder.
    /// </summary>
    [Fact]
    public void EnterOnAnArchiveWalksIntoIt()
    {
        OnTheArchive();
        _app.Press(ConsoleKey.Enter);

        Assert.True(_app.Until(() => _app.Sessions.Left.Source is ArchiveSource));
        Assert.Equal("/", _app.Sessions.Left.Folder);
        Assert.Empty(_app.Opener.Paths);
        Assert.True(_app.Shows("readme.md"));
    }

    /// <summary>A folder inside the archive is walked into as any other folder is.</summary>
    [Fact]
    public void AFolderInsideTheArchiveIsWalkedInto()
    {
        OnTheArchive();
        _app.Press(ConsoleKey.Enter);

        Assert.True(_app.Until(() => _app.Sessions.Left.Source is ArchiveSource));
        Assert.True(_app.Shows("src"));

        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.Enter);

        Assert.True(_app.Until(() => _app.Sessions.Left.Folder == "/src"));
        Assert.True(_app.Shows("main.c"));
    }

    /// <summary>
    /// Leaving the root of an archive puts the panel back in the folder the archive sits in, with the
    /// cursor on the archive. Nothing is above the root of an archive but the file it is.
    /// </summary>
    [Fact]
    public void LeavingTheRootPutsThePanelBackOnTheArchive()
    {
        OnTheArchive();
        _app.Press(ConsoleKey.Enter);

        Assert.True(_app.Until(() => _app.Sessions.Left.Source is ArchiveSource));

        _app.Press(ConsoleKey.LeftArrow);

        Assert.True(_app.Until(() => _app.Sessions.Left.Source is not ArchiveSource));
        Assert.Equal(_app.Folder, _app.Sessions.Left.Folder);
    }

    /// <summary>
    /// The copy key carries a file into the archive the other panel is inside. It is the same key it
    /// is onto a disk, the panel being connected to the archive as it would be to a server.
    /// </summary>
    [Fact]
    public void TheCopyKeyCarriesAFileIntoTheArchiveTheOtherPanelIsIn()
    {
        if (!SevenZip.Works)
        {
            return;
        }

        var archivePath = Path.Combine(_app.Folder, "writable.7z");

        _app.Write("carried.txt", "carried in");
        Ran(SevenZip.Starting("a", archivePath, Path.Combine(_app.Folder, "carried.txt")));

        _app.Sessions.Right.Connect(new ArchiveSource(archivePath, SevenZipArchives.Instance), "/");
        _app.Sessions.Left.GoTo(_app.Folder);
        _app.Settled();

        Assert.True(_app.Shows("bundle.zip"));

        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.F5);
        _app.Frame();
        _app.Press(ConsoleKey.Enter);

        Assert.True(_app.Until(() => Listed(archivePath).Contains("bundle.zip")));
    }

    private static void Ran(System.Diagnostics.ProcessStartInfo start)
    {
        using var running = System.Diagnostics.Process.Start(start)!;

        running.StandardOutput.ReadToEnd();
        running.StandardError.ReadToEnd();
        running.WaitForExit();
    }

    private static string Listed(string archive) =>
        string.Join('\n', SevenZipArchives.Instance.Read(archive).Select(static entry => entry.Path));

    /// <summary>
    /// What is marked is packed into an archive beside it, under a name the dialog offers and the
    /// keyboard can change. The archive is made where the files are rather than on the other panel.
    /// </summary>
    [Fact]
    public void WhatIsMarkedIsPackedIntoAnArchiveBesideIt()
    {
        if (!SevenZip.Works)
        {
            return;
        }

        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.A);
        _app.Press(ConsoleKey.P);
        _app.Frame();

        Assert.Contains("Pack", _app.Frame(), StringComparison.Ordinal);

        _app.Press(ConsoleKey.Enter);

        Assert.True(_app.Until(() => File.Exists(Path.Combine(_app.Folder, "bundle.7z"))));
    }

    /// <summary>
    /// An archive nothing can be written into says so instead of opening the dialog. A question
    /// answered and then refused is worse than one never asked.
    /// </summary>
    [Fact]
    public void AnArchiveThatCannotBeWrittenIntoRefusesBeforeTheDialog()
    {
        var packedPath = Path.Combine(_app.Folder, "bundle.tar.gz");
        var loose = Directory.CreateTempSubdirectory("commander-packed").FullName;

        File.WriteAllText(Path.Combine(loose, "inside.txt"), "what is in it");

        using (var file = File.Create(packedPath))
        using (var zipping = new GZipStream(file, CompressionMode.Compress))
        {
            TarFile.CreateFromDirectory(loose, zipping, includeBaseDirectory: false);
        }

        Directory.Delete(loose, true);

        _app.Sessions.Left.Connect(new ArchiveSource(packedPath, TarArchives.Instance), "/");

        Assert.True(_app.Shows("inside.txt"));

        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.F8);
        _app.Frame();

        Assert.Contains("nothing can be written into it", _app.State.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// An archive under a name that says nothing of it is walked into when asked for by the key. Enter
    /// hands such a file to the desktop, having no way of telling it from any other.
    /// </summary>
    [Fact]
    public void AnArchiveUnderAnyNameIsWalkedIntoByTheKey()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_app.Folder, "unnamed")).FullName;

        File.Copy(Path.Combine(_app.Folder, "bundle.zip"), Path.Combine(folder, "bundle.data"));
        _app.Sessions.Left.GoTo(folder);

        Assert.True(_app.Until(() => _app.Shows("bundle.data")));

        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.A);
        _app.Press(ConsoleKey.O);

        Assert.True(_app.Until(() => _app.Sessions.Left.Source is ArchiveSource));
        Assert.Equal("bundle.data", _app.Sessions.Left.Source.Label);
        Assert.True(_app.Shows("readme.md"));
    }

    /// <summary>A file that is no archive under any reading says so, and the panel stays where it was.</summary>
    [Fact]
    public void AFileThatIsNoArchiveSaysSo()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_app.Folder, "plain")).FullName;

        File.WriteAllText(Path.Combine(folder, "notes.txt"), "nothing packed in here");
        _app.Sessions.Left.GoTo(folder);

        Assert.True(_app.Until(() => _app.Shows("notes.txt")));

        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.A);
        _app.Press(ConsoleKey.O);

        Assert.True(_app.Until(() => _app.State.Output.Contains("would not open", StringComparison.Ordinal)));
        Assert.IsNotType<ArchiveSource>(_app.Sessions.Left.Source);
    }

    /// <summary>
    /// A file inside an archive has no path on this machine, so it is read in the viewer rather than
    /// handed to whatever opens files of its kind.
    /// </summary>
    [Fact]
    public void AFileInsideTheArchiveIsReadInTheViewer()
    {
        OnTheArchive();
        _app.Press(ConsoleKey.Enter);

        Assert.True(_app.Until(() => _app.Sessions.Left.Source is ArchiveSource));
        Assert.True(_app.Shows("readme.md"));

        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.Enter);
        _app.Frame();

        Assert.Equal(ViewKind.Viewer, _app.Navigator.CurrentRoute);
        Assert.Empty(_app.Opener.Paths);
    }
}
