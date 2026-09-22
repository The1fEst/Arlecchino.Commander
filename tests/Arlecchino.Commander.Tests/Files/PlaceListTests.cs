using System.IO;
using Arlecchino.Commander.Files.Disks;
using Arlecchino.Commander.Views.Doing;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// What a disk says about itself beside its name in the list. The room left on it is the one part read
/// off the machine, and it is read off the mount point rather than off a path under it.
/// </summary>
public sealed class PlaceListTests
{
    /// <summary>
    /// A partition that is in the machine and mounted nowhere says so where its room would be. There is
    /// no figure to give for it, and leaving the column empty would read as a disk that is full.
    /// </summary>
    [Fact]
    public void APlaceThatIsNotMountedSaysSoInPlaceOfItsRoom()
    {
        var place = new Place("ESD-USB", "", "/dev/sdd1", "vfat", 16063135744, IsRemovable: true);

        Assert.Equal("vfat · 15 GB · not mounted", PlaceList.Said(place));
    }

    /// <summary>A mounted place says what is on it, how large it is, and how much of it is left.</summary>
    [Fact]
    public void AMountedPlaceSaysWhatIsLeftOnIt()
    {
        var folder = Directory.CreateTempSubdirectory("commander-place").FullName;

        try
        {
            var place = new Place("Data", folder, "/dev/sdb1", "btrfs", 2000397795328, IsRemovable: false);
            var hint = PlaceList.Said(place);

            Assert.StartsWith("btrfs · 1.8 TB · ", hint, System.StringComparison.Ordinal);
            Assert.EndsWith(" free", hint, System.StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder);
        }
    }

    /// <summary>
    /// A place the machine said nothing about beyond where it is carries neither of the two figures,
    /// and what is written is the room alone rather than the separators with nothing between them.
    /// </summary>
    [Fact]
    public void APlaceWithNothingKnownAboutItIsNotWrittenAsEmptyColumns()
    {
        var place = new Place("stick", "", "/dev/sdd1", "", 0, IsRemovable: true);

        Assert.Equal("not mounted", PlaceList.Said(place));
    }
}
