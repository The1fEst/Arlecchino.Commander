using System.Collections.Generic;
using System.Text;
using Arlecchino.Commander.Files.Tty;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// Gathering what a command printed into lines. A terminal hands over bytes in whatever lengths it
/// pleases, and none of the joins between them are line endings.
/// </summary>
public sealed class SaysTests
{
    private readonly Says _says = new();
    private readonly List<string> _lines = [];

    private void Read(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);

        _says.Takes(bytes, bytes.Length, _lines.Add);
    }

    [Fact]
    public void LinesComeOutAsTheyAreFinished()
    {
        Read("one\r\ntwo\r\n");

        Assert.Equal(["one", "two"], _lines);
    }

    [Fact]
    public void AnUnfinishedLineIsHeldRatherThanGivenUp()
    {
        Read("password:");

        Assert.Empty(_lines);
        Assert.Equal("password:", _says.Pending);
    }

    [Fact]
    public void ALineSplitAcrossMouthfulsIsOneLine()
    {
        Read("half");
        Read(" and half\n");

        Assert.Equal(["half and half"], _lines);
    }

    /// <summary>
    /// A letter can be split across two mouthfuls as easily as a line can. Read as bytes, half of one is
    /// a question mark that never goes away.
    /// </summary>
    [Fact]
    public void ALetterSplitAcrossMouthfulsIsOneLetter()
    {
        var bytes = Encoding.UTF8.GetBytes("да\n");

        _says.Takes(bytes[..2], 2, _lines.Add);
        _says.Takes(bytes[2..], bytes.Length - 2, _lines.Add);

        Assert.Equal(["да"], _lines);
    }

    /// <summary>
    /// A count, a bar or a spinner is written over and over onto the one line. What is worth keeping is
    /// where it got to, not every step it took to get there.
    /// </summary>
    [Fact]
    public void ALineDrawnOverItselfIsKeptOnce()
    {
        Read("10%\r55%\r100%\n");

        Assert.Equal(["100%"], _lines);
    }

    [Fact]
    public void WhatIsLeftOverIsGivenUpAtTheEnd()
    {
        Read("no ending");
        _says.Rest(_lines.Add);

        Assert.Equal(["no ending"], _lines);
        Assert.Equal("", _says.Pending);
    }
}
