using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
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
        _app.Press(ConsoleKey.X);
        _app.Press(ConsoleKey.Z);
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
