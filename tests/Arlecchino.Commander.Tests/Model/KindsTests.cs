using Arlecchino.Commander.Model;
using Xunit;

namespace Arlecchino.Commander.Tests.Model;

/// <summary>
/// The three letters a row is tagged with. What matters here is what happens off the edge of the table:
/// a program with nothing in its name to say so, and an extension no family covers.
/// </summary>
public sealed class KindsTests
{
    private static FileEntry File(string name, bool executable = false) =>
        new(name, $"/tmp/{name}", false, false, 0, default, name.StartsWith('.'), false, executable);

    [Fact]
    public void AProgramWithNoExtensionIsNotCalledText()
    {
        Assert.Equal("exe", Kinds.Tag(File("arlc", executable: true)));
    }

    [Fact]
    public void SomethingWithNoExtensionAndNoLeaveToRunIsTaggedWithNothing()
    {
        Assert.Equal("", Kinds.Tag(File("LICENSE")));
    }

    [Fact]
    public void AnExtensionNobodyGroupedIsWrittenAsItIs()
    {
        Assert.Equal("cpp", Kinds.Tag(File("main.cpp")));
        Assert.Equal("py", Kinds.Tag(File("build.py")));
    }

    [Fact]
    public void ALongExtensionIsCutToTheColumn()
    {
        Assert.Equal("bla", Kinds.Tag(File("thing.blabla")));
    }

    [Fact]
    public void AFamilyOfExtensionsSharesOneTag()
    {
        Assert.Equal("img", Kinds.Tag(File("photo.JPEG")));
        Assert.Equal("cfg", Kinds.Tag(File("app.yaml")));
        Assert.Equal("lib", Kinds.Tag(File("libssh.so")));
        Assert.Equal("cfg", Kinds.Tag(File("Arlecchino.Commander.sln.DotSettings.user")));
    }

    [Fact]
    public void WindowsProgramsAreTaggedByTheirExtension()
    {
        Assert.Equal("exe", Kinds.Tag(File("setup.exe")));
        Assert.Equal("exe", Kinds.Tag(File("run.cmd")));
    }

    [Fact]
    public void ANameThatIsAllSecretBeatsWhateverItsExtensionSays()
    {
        Assert.Equal("key", Kinds.Tag(File(".env.local")));
        Assert.Equal("git", Kinds.Tag(File(".gitignore")));
    }

    [Fact]
    public void ALibraryWithTheLeaveToRunIsStillALibrary()
    {
        Assert.Equal("lib", Kinds.Tag(File("libssh.so", executable: true)));
    }

    [Fact]
    public void CertificatesAreDrawnAsQuietlyAsKeys()
    {
        Assert.Equal(Tone.Protected, Kinds.ToneOf(File("server.crt")));
    }
}
