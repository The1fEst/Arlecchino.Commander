using System;
using System.Linq;
using Arlecchino.Commander.Files.Archives;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// What the program writes when it is asked to list an archive, read back as entries. The text here is
/// what it really wrote, so that the reading is held to the program rather than to a guess at it.
/// </summary>
public sealed class SevenZipListingTests
{
    private const string Listing = """
        7-Zip 26.03 (x64) : Copyright (c) 1999-2026 Igor Pavlov : 2026-09-03

        Listing archive: bundle.7z

        --
        Path = bundle.7z
        Type = 7z
        Physical Size = 209

        ----------
        Path = src
        Size = 0
        Packed Size = 0
        Modified = 2026-09-22 21:03:58.6701153
        Attributes = D drwxr-xr-x
        CRC =
        Encrypted = -

        Path = readme.md
        Size = 6
        Packed Size = 27
        Modified = 2026-09-22 21:03:58.6706890
        Attributes = A -rw-r--r--
        CRC = 363A3020
        Encrypted = -

        Path = src/main.c
        Size = 17
        Packed Size =
        Modified = 2026-09-22 21:03:58.6706890
        Attributes = A -rw-r--r--
        CRC = 0FDF1A73
        Encrypted = -
        """;

    [Fact]
    public void EveryEntryOfTheArchiveIsRead()
    {
        var entries = SevenZipListing.Read(Listing);

        Assert.Equal(["src", "readme.md", "src/main.c"], entries.Select(static entry => entry.Path));
    }

    /// <summary>
    /// The header is not entries. It names the archive itself under the same word the entries use, and
    /// taken as one it puts the archive inside itself.
    /// </summary>
    [Fact]
    public void WhatStandsBeforeTheEntriesIsNotOneOfThem()
    {
        Assert.DoesNotContain("bundle.7z", SevenZipListing.Read(Listing).Select(static entry => entry.Path));
    }

    [Fact]
    public void AFileCarriesItsSizeAndTheTimeItWasWritten()
    {
        var file = SevenZipListing.Read(Listing).Single(static entry => entry.Path == "readme.md");

        Assert.False(file.IsFolder);
        Assert.Equal(6, file.Size);
        Assert.Equal(new(2026, 9, 22, 21, 3, 58), file.Modified, TimeSpan.FromSeconds(1));
    }

    /// <summary>A folder is known by the first letter of its attributes, which is where the program puts it.</summary>
    [Fact]
    public void AFolderIsKnownByItsAttributes()
    {
        var entries = SevenZipListing.Read(Listing);

        Assert.True(entries.Single(static entry => entry.Path == "src").IsFolder);
        Assert.False(entries.Single(static entry => entry.Path == "src/main.c").IsFolder);
    }

    /// <summary>
    /// A path with an equals sign in it is read whole. The first one divides the name from the value,
    /// and a file called <c>a = b</c> is a file, not a line setting something.
    /// </summary>
    [Fact]
    public void APathWithAnEqualsSignInItIsReadWhole()
    {
        var listing = "----------\nPath = odd = name.txt\nSize = 3\nAttributes = A\n";

        Assert.Equal("odd = name.txt", SevenZipListing.Read(listing).Single().Path);
    }

    /// <summary>An archive the program found nothing in is no entries rather than one empty one.</summary>
    [Fact]
    public void AnArchiveWithNothingInItIsNoEntriesAtAll()
    {
        Assert.Empty(SevenZipListing.Read("----------\n"));
        Assert.Empty(SevenZipListing.Read(""));
    }
}
