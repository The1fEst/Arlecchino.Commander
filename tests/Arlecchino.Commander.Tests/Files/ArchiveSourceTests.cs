using System;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Archives;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Files.Work;
using Xunit;
using Xunit.Abstractions;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// A zip browsed as though it were a folder. What is asserted is a real archive written to a real file,
/// since what this has to get right is the format rather than the shape of the calls.
/// </summary>
public sealed class ArchiveSourceTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("commander-archive").FullName;

    private readonly ITestOutputHelper _output;

    public ArchiveSourceTests(ITestOutputHelper output) => _output = output;

    public void Dispose() => Directory.Delete(_folder, true);

    /// <summary>
    /// Whether the machine running the tests has the program the packed formats are handed to. Without
    /// it there is nothing to hold to account, and the run says so rather than failing.
    /// </summary>
    /// <returns><c>true</c> when the test is to be passed over.</returns>
    private bool PassedOver()
    {
        if (ToolArchives.Works)
        {
            return false;
        }

        _output.WriteLine($"Passed over: this machine has no {ToolArchives.Program}.");

        return true;
    }

    private string Zipped(string name, params (string Path, string Text)[] entries)
    {
        var path = Path.Combine(_folder, name);

        Written(path, entries);

        return path;
    }

    private string Tarred(string name, params (string Path, string Text)[] entries)
    {
        var path = Path.Combine(_folder, name);
        var loose = Path.Combine(_folder, "loose");

        Directory.CreateDirectory(loose);

        foreach (var (inside, text) in entries)
        {
            var file = Path.Combine(loose, inside);

            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, text);
        }

        using (var writing = File.Create(path))
        {
            if (name.EndsWith(".gz", StringComparison.Ordinal))
            {
                using var zipping = new GZipStream(writing, CompressionMode.Compress);

                TarFile.CreateFromDirectory(loose, zipping, includeBaseDirectory: false);
            }
            else
            {
                TarFile.CreateFromDirectory(loose, writing, includeBaseDirectory: false);
            }
        }

        Directory.Delete(loose, true);

        return path;
    }

    /// <summary>
    /// A tar packed with something the runtime cannot unpack, made by the same program that reads it.
    /// Writing one by hand would be writing the format this is meant to be testing the reading of.
    /// </summary>
    /// <param name="name">What the archive is called, which is what says how it is packed.</param>
    /// <param name="entries">What to put in it.</param>
    /// <returns>The archive, as a path on this disk.</returns>
    private string Repacked(string name, params (string Path, string Text)[] entries)
    {
        var plain = Tarred("plain.tar", entries);
        var path = Path.Combine(_folder, name);
        var start = new ProcessStartInfo(ToolArchives.Program)
        {
            UseShellExecute = false,
            WorkingDirectory = _folder,
            ArgumentList = { "-c", "-a", "-f", path, "@" + plain },
        };

        using (var running = Process.Start(start)!)
        {
            running.WaitForExit();

            Assert.Equal(0, running.ExitCode);
        }

        File.Delete(plain);

        return path;
    }

    private static void Written(string path, (string Path, string Text)[] entries)
    {
        using var file = File.Create(path);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);

        foreach (var (inside, text) in entries)
        {
            using var writing = new StreamWriter(zip.CreateEntry(inside).Open(), new UTF8Encoding(false));

            writing.Write(text);
        }
    }

    [Fact]
    public async Task TheRootOfTheArchiveListsWhatIsInIt()
    {
        var path = Zipped("bundle.zip", ("readme.md", "hello"), ("src/main.c", "int main(void){}"));

        using var source = new ArchiveSource(path, ZipArchives.Instance);
        var rows = await source.ListAsync("/", showHidden: true, CancellationToken.None);
        var names = rows.Where(static row => !row.IsParent).Select(static row => row.Name).Order().ToArray();

        Assert.Equal(["readme.md", "src"], names);
        Assert.Equal("bundle.zip", source.Label);
    }

    /// <summary>
    /// A file is read out of the archive as a stream, which is what lets copying out of one be the same
    /// work as copying off a disk.
    /// </summary>
    [Fact]
    public async Task AFileIsReadOutOfTheArchive()
    {
        var path = Zipped("bundle.zip", ("src/main.c", "int main(void){}"));

        using var source = new ArchiveSource(path, ZipArchives.Instance);

        await using var reading = await source.OpenReadAsync("/src/main.c", CancellationToken.None);
        using var text = new StreamReader(reading);

        Assert.Equal("int main(void){}", await text.ReadToEndAsync());
    }

    /// <summary>
    /// The archive stays open behind the stream. A zip unpacks as it is read, so closing it on the way
    /// out of the call that opened the entry would leave the reader with nothing to read from.
    /// </summary>
    [Fact]
    public async Task TheArchiveOutlivesTheStreamTakenOutOfIt()
    {
        var path = Zipped("bundle.zip", ("long.txt", new('x', 200_000)));

        using var source = new ArchiveSource(path, ZipArchives.Instance);

        await using var reading = await source.OpenReadAsync("/long.txt", CancellationToken.None);
        var copy = new MemoryStream();

        await reading.CopyToAsync(copy);

        Assert.Equal(200_000, copy.Length);
    }

    /// <summary>
    /// A whole folder is carried out of the archive onto the disk, which is what the copy key comes to
    /// once a panel is inside one. An archive is a source like any other here.
    /// </summary>
    [Fact]
    public async Task AFolderIsCarriedOutOfTheArchiveOntoTheDisk()
    {
        var path = Zipped("bundle.zip", ("src/main.c", "int main(void){}"), ("src/deep/other.c", "void f(){}"));
        var landing = Directory.CreateDirectory(Path.Combine(_folder, "out")).FullName;
        var outcome = new Outcome();

        using var source = new ArchiveSource(path, ZipArchives.Instance);
        using var disk = new LocalSource();

        var rows = await source.ListAsync("/", showHidden: true, CancellationToken.None);
        var folder = rows.Single(static row => row.Name == "src");

        await FileTasks.CopyAsync(source, [folder], disk, landing, outcome, CancellationToken.None);

        Assert.False(outcome.Failed);
        Assert.Equal("int main(void){}",
            await File.ReadAllTextAsync(Path.Combine(landing, "src", "main.c")));
        Assert.Equal("void f(){}",
            await File.ReadAllTextAsync(Path.Combine(landing, "src", "deep", "other.c")));
    }

    /// <summary>Writing into an archive is refused in words rather than half done.</summary>
    [Fact]
    public async Task NothingCanBeWrittenIntoAnArchive()
    {
        var path = Zipped("bundle.zip", ("readme.md", "hello"));

        using var source = new ArchiveSource(path, ZipArchives.Instance);
        var error = await Assert.ThrowsAsync<IOException>(() => source.CreateAsync("/new.txt", CancellationToken.None));

        Assert.Contains("bundle.zip", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A file that names itself an archive and is not one opens as nothing at all.</summary>
    [Fact]
    public void AFileThatIsNoArchiveWillNotOpen()
    {
        var path = Path.Combine(_folder, "broken.zip");

        File.WriteAllText(path, "this is not a zip");

        Assert.Null(Archives.Open(path));
    }

    /// <summary>
    /// The document formats that are zips underneath are left alone. Opening one means the document,
    /// and browsing its parts is not what anyone pressed the key for.
    /// </summary>
    /// <param name="name">The file.</param>
    [Theory]
    [InlineData("notes.docx")]
    [InlineData("sheet.xlsx")]
    [InlineData("book.epub")]
    public void ADocumentThatIsAZipUnderneathIsNotOpenedAsOne(string name)
    {
        Assert.Null(Archives.For(name));
    }

    [Theory]
    [InlineData("bundle.zip")]
    [InlineData("library.jar")]
    [InlineData("package.nupkg")]
    [InlineData("source.tar")]
    [InlineData("source.tar.gz")]
    [InlineData("source.tgz")]
    public void TheArchivesWorthWalkingIntoAreOpened(string name)
    {
        Assert.NotNull(Archives.For(name));
    }

    /// <summary>
    /// A tar packed with <c>gzip</c> is read as the tar it is. The name ends in both, and the outer
    /// format alone makes a single entry of a whole tree.
    /// </summary>
    [Fact]
    public void AGzippedTarIsReadAsTheTarItIs()
    {
        Assert.Same(TarArchives.Instance, Archives.For("source.tar.gz"));
        Assert.Same(TarArchives.Instance, Archives.For("source.tgz"));
    }

    /// <summary>
    /// The formats the runtime cannot read are handed to a program that can, on a machine that has
    /// one. Without it, they are not offered at all and the key hands the file to the desktop.
    /// </summary>
    /// <param name="name">The file.</param>
    [Theory]
    [InlineData("source.tar.xz")]
    [InlineData("source.tar.zst")]
    [InlineData("bundle.7z")]
    [InlineData("bundle.rar")]
    public void TheFormatsTheRuntimeCannotReadGoToAProgram(string name)
    {
        Assert.Equal(ToolArchives.Works, Archives.For(name) is ToolArchives);
    }

    /// <summary>
    /// A packed tar this machine has a program for is walked and read through as any other is. The
    /// program writes it back out as a plain tar, which is the one walk there is.
    /// </summary>
    [Fact]
    public async Task APackedTarIsWalkedThroughTheProgramThatReadsIt()
    {
        if (PassedOver())
        {
            return;
        }

        var path = Repacked("source.tar.xz", ("src/main.c", "int main(void){}"), ("readme.md", "hello"));

        using var source = new ArchiveSource(path, ToolArchives.Instance);
        var rows = await source.ListAsync("/", showHidden: true, CancellationToken.None);
        var names = rows.Where(static row => !row.IsParent).Select(static row => row.Name).Order().ToArray();

        Assert.Equal(["readme.md", "src"], names);

        await using var reading = await source.OpenReadAsync("/src/main.c", CancellationToken.None);
        using var text = new StreamReader(reading);

        Assert.Equal("int main(void){}", await text.ReadToEndAsync());
    }

    /// <summary>A file the program will not read opens as nothing, rather than as an empty archive.</summary>
    [Fact]
    public void AFileTheProgramWillNotReadOpensAsNothing()
    {
        if (PassedOver())
        {
            return;
        }

        var path = Path.Combine(_folder, "broken.7z");

        File.WriteAllText(path, "this is not an archive of any kind");

        Assert.Null(Archives.Open(path));
    }

    /// <summary>A tar is walked as a zip is, folders and all, and a file comes out of it whole.</summary>
    /// <param name="name">What the archive is called, which is what says whether it was packed.</param>
    [Theory]
    [InlineData("source.tar")]
    [InlineData("source.tar.gz")]
    public async Task ATarIsWalkedAndReadThroughAsAZipIs(string name)
    {
        var path = Tarred(name, ("src/main.c", "int main(void){}"), ("readme.md", "hello"));

        using var source = new ArchiveSource(path, TarArchives.Instance);
        var rows = await source.ListAsync("/", showHidden: true, CancellationToken.None);
        var names = rows.Where(static row => !row.IsParent).Select(static row => row.Name).Order().ToArray();

        Assert.Equal(["readme.md", "src"], names);

        await using var reading = await source.OpenReadAsync("/src/main.c", CancellationToken.None);
        using var text = new StreamReader(reading);

        Assert.Equal("int main(void){}", await text.ReadToEndAsync());
    }
}
