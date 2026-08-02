using System;
using Arlecchino.Commander.Files.Sources;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

public sealed class FtpListingTests
{
    [Fact]
    public void AMachineListingSaysWhatEachEntryIs()
    {
        var entries = FtpListings.Machine(string.Join(
            "\r\n",
            "type=cdir;modify=20240101120000; /pub",
            "type=pdir;modify=20240101120000; /",
            "type=dir;sizd=4096;modify=20230515093000;UNIX.mode=0755; docs",
            "type=file;size=1234;modify=20240102153045;UNIX.mode=0644; readme.txt"));

        Assert.Equal(2, entries.Count);
        Assert.True(entries[0].IsFolder);
        Assert.Equal("docs", entries[0].Name);
        Assert.Equal(755, entries[0].Mode);
        Assert.Equal(1234, entries[1].Size);
        Assert.Equal(644, entries[1].Mode);
        Assert.Equal(2024, entries[1].Modified.Year);
    }

    /// <summary>
    /// The folder itself and its parent come back as entries of their own, marked <c>cdir</c> and
    /// <c>pdir</c>. A panel adds its own way up, so listing them again would show it twice.
    /// </summary>
    [Fact]
    public void AMachineListingLeavesOutTheFolderItself()
    {
        var entries = FtpListings.Machine(string.Join(
            "\r\n",
            "type=cdir; /pub",
            "type=pdir; /",
            "type=file;size=1; kept.txt"));

        Assert.Equal("kept.txt", Assert.Single(entries).Name);
    }

    [Fact]
    public void AFolderInAMachineListingHasNoSizeOfItsOwn()
    {
        var entries = FtpListings.Machine("type=dir;size=4096; docs");

        Assert.Equal(0, Assert.Single(entries).Size);
    }

    [Fact]
    public void ANameWithSpacesSurvivesAMachineListing()
    {
        var entries = FtpListings.Machine("type=file;size=1; two words.txt");

        Assert.Equal("two words.txt", Assert.Single(entries).Name);
    }

    [Fact]
    public void APlainListingIsReadTheWayLsWritesIt()
    {
        var entries = FtpListings.Plain(string.Join(
            "\r\n",
            "drwxr-xr-x   2 ftp      ftp          4096 Jan 01 12:00 docs",
            "-rw-r--r--   1 ftp      ftp          1234 May 15  2023 readme.txt",
            "-rw-r--r--   1 ftp      ftp           100 Jan 01 12:00 two words.txt"));

        Assert.Equal(3, entries.Count);
        Assert.True(entries[0].IsFolder);
        Assert.Equal(755, entries[0].Mode);
        Assert.Equal(1234, entries[1].Size);
        Assert.Equal(644, entries[1].Mode);
        Assert.Equal("two words.txt", entries[2].Name);
    }

    /// <summary>
    /// A link is listed as where it points, which is not part of its name — the panel needs the name
    /// to ask about it again.
    /// </summary>
    [Fact]
    public void ALinkIsNamedWithoutWhatItPointsAt()
    {
        var entries = FtpListings.Plain("lrwxrwxrwx   1 ftp      ftp             7 Jan 01 12:00 link -> target");

        Assert.Equal("link", Assert.Single(entries).Name);
    }

    /// <summary>
    /// Something changed within the last half year is written with the time and no year at all, so
    /// the year has to be worked out — and a listing cannot hold tomorrow.
    /// </summary>
    [Fact]
    public void ADateWithoutAYearIsPutInThePast()
    {
        var soon = DateTime.Now.AddMonths(2);
        var entries = FtpListings.Plain(
            $"-rw-r--r--   1 ftp ftp 1 {soon:MMM} {soon.Day:00} 12:00 later.txt");

        Assert.True(Assert.Single(entries).Modified < DateTime.Now.AddDays(1));
    }

    [Fact]
    public void ADateWithAYearIsTakenAsWritten()
    {
        var entries = FtpListings.Plain("-rw-r--r--   1 ftp ftp 1 May 15  2023 old.txt");

        Assert.Equal(new(2023, 5, 15), Assert.Single(entries).Modified);
    }

    [Fact]
    public void AWindowsListingIsReadToo()
    {
        var entries = FtpListings.Plain(string.Join(
            "\r\n",
            "01-01-24  12:00PM       <DIR>          docs",
            "05-15-23  09:30AM                 1234 readme.txt"));

        Assert.Equal(2, entries.Count);
        Assert.True(entries[0].IsFolder);
        Assert.Equal("docs", entries[0].Name);
        Assert.Equal(1234, entries[1].Size);
    }

    [Fact]
    public void AListingItCannotReadIsSkippedRatherThanGuessedAt()
    {
        Assert.Empty(FtpListings.Plain("total 8\r\nsomething else entirely"));
    }

    [Fact]
    public void TheExtendedPortIsReadFromBetweenTheServersOwnDelimiters()
    {
        Assert.Equal(6446, FtpListings.ExtendedPort("229 Entering Extended Passive Mode (|||6446|)"));
        Assert.Equal(0, FtpListings.ExtendedPort("229 nothing in brackets"));
    }

    /// <summary>The port is the last two of six numbers, high byte first.</summary>
    [Fact]
    public void ThePassivePortIsTwoNumbersPutTogether()
    {
        Assert.Equal((25 * 256) + 14, FtpListings.PassivePort("227 Entering Passive Mode (10,0,0,1,25,14)"));
        Assert.Equal(0, FtpListings.PassivePort("227 nothing in brackets"));
        Assert.Equal(0, FtpListings.PassivePort("227 (1,2,3)"));
    }

    [Fact]
    public void ARefusalIsNotMistakenForAnAnswer()
    {
        Assert.False(new FtpReply(530, "Login incorrect").Worked);
        Assert.True(new FtpReply(226, "that was all").Worked);
        Assert.True(new FtpReply(150, "here it comes").Worked);
    }

    [Fact]
    public void NothingListedIsNothingRead()
    {
        Assert.Empty(FtpListings.Machine(""));
        Assert.Empty(FtpListings.Plain(""));
    }
}
