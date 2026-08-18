using Arlecchino.Commander.Model;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// The pattern a filter or a search is given, where the edges matter more than the ordinary cases.
/// </summary>
public sealed class GlobTests
{
    [Theory]
    [InlineData("notes.txt", "*.txt")]
    [InlineData("notes.txt", "*")]
    [InlineData("notes.txt", "notes.???")]
    [InlineData("notes.txt", "n*s.txt")]
    [InlineData("NOTES.TXT", "*.txt")]
    [InlineData("notes.txt", "*.md, *.txt")]
    public void APatternMatchesWhatItDescribes(string name, string pattern)
    {
        Assert.True(Glob.Matches(name, pattern));
    }

    [Theory]
    [InlineData("notes.txt", "*.md")]
    [InlineData("notes.txt", "notes.??")]
    [InlineData("notes.txt.bak", "*.txt")]
    [InlineData("notes.txt", "")]
    [InlineData("notes.txt", "otes.txt")]
    public void APatternDoesNotMatchWhatItDoesNot(string name, string pattern)
    {
        Assert.False(Glob.Matches(name, pattern));
    }

    /// <summary>
    /// A pattern is not a regular expression, whatever it looks like: the characters a regular
    /// expression would read as instructions have to stand for themselves, or a filter typed with a dot
    /// in it would match everything.
    /// </summary>
    [Theory]
    [InlineData("a.txt", "?.txt", true)]
    [InlineData("ab.txt", ".*", false)]
    [InlineData("notes(1).txt", "notes(1).txt", true)]
    [InlineData("notes1.txt", "notes(1).txt", false)]
    [InlineData("a+b.txt", "a+b.txt", true)]
    [InlineData("aab.txt", "a+b.txt", false)]
    public void TheCharactersAnExpressionWouldReadAreTakenLiterally(string name, string pattern, bool matches)
    {
        Assert.Equal(matches, Glob.Matches(name, pattern));
    }

    [Fact]
    public void EmptyPiecesInAListAreSkippedRatherThanMatchingEverything()
    {
        Assert.True(Glob.Matches("notes.txt", "*.txt,,"));
        Assert.False(Glob.Matches("notes.md", ",,"));
    }

    [Fact]
    public void SpacesAroundAPieceAreNotPartOfIt()
    {
        Assert.True(Glob.Matches("notes.txt", " *.txt , *.md "));
    }

    [Theory]
    [InlineData("notes", "*notes*")]
    [InlineData("notes, todo", "*notes*,*todo*")]
    [InlineData("*.txt", "*.txt")]
    [InlineData("notes.???", "notes.???")]
    [InlineData("notes, *.txt", "*notes*,*.txt")]
    [InlineData("", "*")]
    [InlineData(" , ", "*")]
    public void APieceWithNoWildcardsInItStandsForThatMuchOfAName(string text, string pattern)
    {
        Assert.Equal(pattern, Glob.Anywhere(text));
    }

    [Fact]
    public void WhatWasTypedIsThenFoundWhereverItSitsInTheName()
    {
        Assert.True(Glob.Matches("my-notes.txt", Glob.Anywhere("notes")));
        Assert.True(Glob.Matches("NOTES.TXT", Glob.Anywhere("notes")));
        Assert.False(Glob.Matches("todo.txt", Glob.Anywhere("notes")));
    }
}
