using System;
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

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// A zip browsed as though it were a folder. What is asserted is a real archive written to a real file,
/// since what this has to get right is the format rather than the shape of the calls.
/// </summary>
public sealed class ArchiveSourceTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("commander-archive").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    private string Zipped(string name, params (string Path, string Text)[] entries)
    {
        var path = Path.Combine(_folder, name);

        Written(path, entries);

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
    public void TheArchivesWorthWalkingIntoAreOpened(string name)
    {
        Assert.NotNull(Archives.For(name));
    }
}
