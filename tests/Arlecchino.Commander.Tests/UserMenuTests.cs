using System;
using Arlecchino.Commander.Files;
using Xunit;

namespace Arlecchino.Commander.Tests;

/// <summary>
/// The user menu, whose entries are commands with the current file written into them. A name with a
/// space in it that reaches a shell unquoted is two arguments, so the quoting is the part that matters.
/// </summary>
public sealed class UserMenuTests
{
    [Fact]
    public void EveryPlaceholderIsFilledWithWhatItStandsFor()
    {
        var filled = UserMenu.Fill(
            "cp %f %D",
            "notes.txt",
            "notes.txt other.txt",
            "/home/someone",
            "/srv/files",
            "there.txt");

        Assert.Equal("cp notes.txt /srv/files", filled);
    }

    [Fact]
    public void TheMarkedFilesGoInAsTheyWereGiven()
    {
        Assert.Equal("rm one.txt two.txt", UserMenu.Fill("rm %t", "one.txt", "one.txt two.txt", "", "", ""));
    }

    [Fact]
    public void TheFolderAndTheOtherPanelAreBothAvailable()
    {
        var filled = UserMenu.Fill("diff %d %D", "", "", "/left", "/right", "");

        Assert.Equal("diff /left /right", filled);
    }

    [Fact]
    public void TheFileOnTheOtherSideHasItsOwnPlaceholder()
    {
        Assert.Equal("diff a.txt b.txt", UserMenu.Fill("diff %f %F", "a.txt", "", "", "", "b.txt"));
    }

    /// <summary>A name with a space in it reaches the shell as one argument or as two, and two is wrong.</summary>
    [Theory]
    [InlineData("notes.txt", "notes.txt")]
    [InlineData("my notes.txt", "\"my notes.txt\"")]
    [InlineData("", "")]
    public void AnythingWithASpaceInItIsQuoted(string piece, string expected)
    {
        Assert.Equal(expected, UserMenu.Quoted(piece));
    }

    [Fact]
    public void AFileWithASpaceInItIsQuotedWhereItLands()
    {
        var filled = UserMenu.Fill("open %f", "my notes.txt", "", "", "", "");

        Assert.Equal("open \"my notes.txt\"", filled);
    }

    [Fact]
    public void ACommandWithNoPlaceholdersIsLeftAlone()
    {
        Assert.Equal("git status", UserMenu.Fill("git status", "notes.txt", "", "/home", "", ""));
    }

    [Fact]
    public void TheMenuLivesWhereTheApplicationKeepsItsSettings()
    {
        Assert.Contains("arlecchino-commander", UserMenu.Location, StringComparison.Ordinal);
        Assert.EndsWith("menu", UserMenu.Location, StringComparison.Ordinal);
    }
}
