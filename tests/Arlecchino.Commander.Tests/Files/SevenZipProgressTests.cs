using Arlecchino.Commander.Files.Archives;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// How far the program says it has got, read off the line it draws. The lines here are what it really
/// drew, backspaces taken out, so the reading is held to the program rather than to a guess at it.
/// </summary>
public sealed class SevenZipProgressTests
{
    [Fact]
    public void ThePercentageAndTheFileAreBothRead()
    {
        var step = SevenZipProgress.Step(" 82% 2 + big/f3.bin");

        Assert.NotNull(step);
        Assert.Equal(82, step.Value.Percent);
        Assert.Equal("big/f3.bin", step.Value.Name);
    }

    /// <summary>The first line it draws is a percentage and nothing else, before it has opened a file.</summary>
    [Fact]
    public void APercentageWithNoFileBehindItIsStillAStep()
    {
        var step = SevenZipProgress.Step("  0%");

        Assert.NotNull(step);
        Assert.Equal(0, step.Value.Percent);
        Assert.Equal("", step.Value.Name);
    }

    /// <summary>A name with spaces in it is read whole, the file being whatever follows the letter.</summary>
    [Fact]
    public void ANameWithSpacesInItIsReadWhole()
    {
        Assert.Equal("My Holiday Photos/last summer.jpg",
            SevenZipProgress.Step(" 41% 7 + My Holiday Photos/last summer.jpg")?.Name);
    }

    /// <summary>
    /// What the program says about itself is not progress. It writes its name, what it is doing and
    /// what it found before it starts, and none of those lines say how far along anything is.
    /// </summary>
    /// <param name="line">A line it wrote.</param>
    [Theory]
    [InlineData("7-Zip 26.03 (x64) : Copyright (c) 1999-2026 Igor Pavlov")]
    [InlineData("Scanning the drive:")]
    [InlineData("Creating archive: bundle.7z")]
    [InlineData("Add new data to archive: 1 folder, 3 files, 9000000 bytes (8790 KiB)")]
    [InlineData("Everything is Ok")]
    [InlineData("")]
    public void WhatIsNotProgressIsNotReadAsProgress(string line)
    {
        Assert.Null(SevenZipProgress.Step(line));
    }

    /// <summary>A percentage outside the hundred is not one, whatever else the line happens to hold.</summary>
    [Fact]
    public void ANumberThatIsNoPercentageIsNotOne()
    {
        Assert.Null(SevenZipProgress.Step("8790 KiB, 101% of nothing"));
    }
}
