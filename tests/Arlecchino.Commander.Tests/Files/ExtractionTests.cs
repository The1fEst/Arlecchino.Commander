using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Archives;
using Arlecchino.Commander.Files.Work;
using Xunit;
using Xunit.Abstractions;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// Whole archives unpacked onto the disk. What is asserted is where everything ends up, since the one
/// thing unpacking must never do is spill an archive over what the folder already holds.
/// </summary>
public sealed class ExtractionTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("commander-extraction").FullName;

    private readonly ITestOutputHelper _output;

    public ExtractionTests(ITestOutputHelper output) => _output = output;

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

    /// <summary>A loose tree to pack, two files at its top and one in a folder under them.</summary>
    /// <returns>The folder holding it.</returns>
    private string Loose()
    {
        var loose = Directory.CreateDirectory(Path.Combine(_folder, "loose")).FullName;

        Directory.CreateDirectory(Path.Combine(loose, "src"));
        File.WriteAllText(Path.Combine(loose, "readme.md"), "read me");
        File.WriteAllText(Path.Combine(loose, "src", "main.c"), "int main;");

        return loose;
    }

    /// <summary>
    /// A single thing at the top of an archive lands as it is. A folder named after the archive would
    /// only hold that one folder again.
    /// </summary>
    [Fact]
    public void ASingleThingAtTheTopLandsAsItIs()
    {
        Assert.Equal(_folder, Extraction.Landing(_folder, "bundle.zip", ["bundle"]));
    }

    /// <summary>A single thing already in the folder is not written over, and goes in a folder of its own.</summary>
    [Fact]
    public void ASingleThingTheFolderHasAlreadyGetsAFolderOfItsOwn()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "bundle"));

        Assert.Equal(
            Path.Combine(_folder, "bundle 2"),
            Extraction.Landing(_folder, "bundle.zip", ["bundle"]));
    }

    /// <summary>Several things at the top go in a folder named after the archive, so none of them spill.</summary>
    [Fact]
    public void SeveralThingsGoInAFolderNamedAfterTheArchive()
    {
        Assert.Equal(
            Path.Combine(_folder, "bundle"),
            Extraction.Landing(_folder, "bundle.tar.xz", ["readme.md", "src"]));
    }

    /// <param name="archive">What the archive is called.</param>
    /// <param name="stem">What the folder named after it is called.</param>
    [Theory]
    [InlineData("bundle.zip", "bundle")]
    [InlineData("bundle.tar.xz", "bundle")]
    [InlineData("bundle.tgz", "bundle")]
    [InlineData("release-1.2.7z", "release-1.2")]
    [InlineData(".zip", "archive")]
    public void TheFolderIsNamedAfterTheArchiveWithoutItsEndings(string archive, string stem)
    {
        Assert.Equal(stem, Extraction.Stem(archive));
    }

    /// <param name="archive">What the archive is called.</param>
    /// <param name="packed">Whether it is a tar inside another format.</param>
    [Theory]
    [InlineData("bundle.tar.gz", true)]
    [InlineData("bundle.TGZ", true)]
    [InlineData("bundle.tar.zst", true)]
    [InlineData("bundle.tar", false)]
    [InlineData("bundle.zip", false)]
    [InlineData("bundle.7z", false)]
    public void ATarInsideAnotherFormatIsKnownByName(string archive, bool packed)
    {
        Assert.Equal(packed, Extraction.Packed(archive));
    }

    /// <summary>A zip with several things at its top is unpacked into a folder named after it.</summary>
    [Fact]
    public async Task AZipIsUnpackedIntoAFolderNamedAfterIt()
    {
        if (PassedOver())
        {
            return;
        }

        var archive = Path.Combine(_folder, "bundle.zip");
        var landing = Directory.CreateDirectory(Path.Combine(_folder, "out")).FullName;

        await ZipFile.CreateFromDirectoryAsync(Loose(), archive);

        var outcome = new Outcome();

        await Extraction.UnpackAsync([archive], landing, outcome, CancellationToken.None);

        Assert.False(outcome.Failed, string.Join("; ", outcome.Errors));
        Assert.Equal("read me", await File.ReadAllTextAsync(Path.Combine(landing, "bundle", "readme.md")));
        Assert.Equal("int main;", await File.ReadAllTextAsync(Path.Combine(landing, "bundle", "src", "main.c")));
    }

    /// <summary>
    /// A tar packed with <c>gzip</c> comes out as the files in it rather than as the tar. A single run of
    /// the program takes the outer layer off and stops there.
    /// </summary>
    [Fact]
    public async Task AGzippedTarComesOutAsItsFilesRatherThanAsATar()
    {
        if (PassedOver())
        {
            return;
        }

        var archive = Path.Combine(_folder, "bundle.tar.gz");
        var landing = Directory.CreateDirectory(Path.Combine(_folder, "out")).FullName;
        var loose = Loose();

        await using (var file = File.Create(archive))
        await using (var zipping = new GZipStream(file, CompressionMode.Compress))
        {
            await TarFile.CreateFromDirectoryAsync(loose, zipping, includeBaseDirectory: false);
        }

        var outcome = new Outcome();

        await Extraction.UnpackAsync([archive], landing, outcome, CancellationToken.None);

        Assert.False(outcome.Failed, string.Join("; ", outcome.Errors));
        Assert.Equal("int main;", await File.ReadAllTextAsync(Path.Combine(landing, "bundle", "src", "main.c")));
        Assert.False(File.Exists(Path.Combine(landing, "bundle", "bundle.tar")));
    }

    /// <summary>A file that is no archive is reported by name, and the rest are unpacked all the same.</summary>
    [Fact]
    public async Task AFileThatIsNoArchiveIsReportedAndTheRestUnpacked()
    {
        if (PassedOver())
        {
            return;
        }

        var archive = Path.Combine(_folder, "bundle.zip");
        var plain = Path.Combine(_folder, "notes.txt");
        var landing = Directory.CreateDirectory(Path.Combine(_folder, "out")).FullName;

        await ZipFile.CreateFromDirectoryAsync(Loose(), archive);
        await File.WriteAllTextAsync(plain, "nothing packed in here");

        var outcome = new Outcome();

        await Extraction.UnpackAsync([plain, archive], landing, outcome, CancellationToken.None);

        Assert.Contains(outcome.Errors, static error => error.Contains("notes.txt", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(landing, "bundle", "readme.md")));
    }
}
