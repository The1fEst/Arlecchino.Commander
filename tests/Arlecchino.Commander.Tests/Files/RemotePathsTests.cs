using Arlecchino.Commander.Files.Sources;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// Paths on the far side, which are not the paths this machine has. A server speaks slashes whatever
/// the client runs on, so none of this may go through the platform's own path handling.
/// </summary>
public sealed class RemotePathsTests
{
    [Theory]
    [InlineData("/home/someone", "notes.txt", "/home/someone/notes.txt")]
    [InlineData("/", "notes.txt", "/notes.txt")]
    [InlineData("/home/someone/", "notes.txt", "/home/someone/notes.txt")]
    public void JoiningPutsExactlyOneSlashBetween(string folder, string name, string expected)
    {
        Assert.Equal(expected, RemotePaths.Combine(folder, name));
    }

    [Theory]
    [InlineData("/home/someone/notes", "/home/someone")]
    [InlineData("/home/someone/notes/", "/home/someone")]
    [InlineData("/home", "/")]
    [InlineData("/home/", "/")]
    public void TheParentIsTheFolderAbove(string folder, string expected)
    {
        Assert.Equal(expected, RemotePaths.Parent(folder));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("")]
    [InlineData("home")]
    public void AboveTheTopThereIsNothing(string folder)
    {
        Assert.Null(RemotePaths.Parent(folder));
    }

    [Theory]
    [InlineData("/home/someone/notes.txt", "notes.txt")]
    [InlineData("/home/someone/notes/", "notes")]
    [InlineData("notes.txt", "notes.txt")]
    [InlineData("/notes.txt", "notes.txt")]
    public void TheNameIsTheLastPiece(string path, string expected)
    {
        Assert.Equal(expected, RemotePaths.NameOf(path));
    }

    [Theory]
    [InlineData(".ssh", true)]
    [InlineData("notes.txt", false)]
    [InlineData("..", true)]
    public void AHiddenNameIsOneThatStartsWithADot(string name, bool hidden)
    {
        Assert.Equal(hidden, RemotePaths.IsHidden(name));
    }
}
