using System.IO;
using Arlecchino.Commander.Files.Work;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// Permission digits as they are typed and as they are sent. Two numbers live in the same three
/// characters here — what the digits stand for, and the digits themselves — and mixing them up is how
/// a file ends up world-writable.
/// </summary>
public sealed class ModesTests
{
    [Theory]
    [InlineData("644", 420)]
    [InlineData("755", 493)]
    [InlineData("000", 0)]
    [InlineData("777", 511)]
    [InlineData("1777", 1023)]
    [InlineData(" 644 ", 420)]
    public void DigitsAreReadAsTheNumberTheyStandFor(string mode, int expected)
    {
        Assert.Equal(expected, Modes.Read(mode));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("648")]
    [InlineData("64x")]
    [InlineData("12345")]
    [InlineData("-644")]
    public void AnythingElseIsRefused(string mode)
    {
        Assert.Null(Modes.Read(mode));
    }

    /// <summary>
    /// What goes over SFTP and FTP is the digits themselves, spelled out again on the far side — so
    /// <c>644</c> travels as six hundred and forty-four, not as the four hundred and twenty it means.
    /// </summary>
    [Theory]
    [InlineData("644", 644)]
    [InlineData("755", 755)]
    [InlineData("007", 7)]
    public void WhatTravelsIsTheDigitsRatherThanWhatTheyMean(string mode, int expected)
    {
        Assert.Equal(expected, Modes.AsDigits(mode));
    }

    [Fact]
    public void WhatIsRefusedIsRefusedTheSameWayBothTimes()
    {
        Assert.Null(Modes.AsDigits("648"));
    }

    [Theory]
    [InlineData(420, "644")]
    [InlineData(493, "755")]
    [InlineData(0, "000")]
    public void WritingPutsTheDigitsBack(int mode, string expected)
    {
        Assert.Equal(expected, Modes.Write(mode));
    }

    [Fact]
    public void ReadingAndWritingComeBackToWhereTheyStarted()
    {
        Assert.Equal("644", Modes.Write(Modes.Read("644")!.Value));
    }

    [Fact]
    public void TheNumberIsTheOneTheOperatingSystemUses()
    {
        var mode = Modes.Read("644")!.Value;

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead,
            Modes.AsUnix(mode));

        Assert.Equal(mode, Modes.FromUnix(Modes.AsUnix(mode)));
    }
}
