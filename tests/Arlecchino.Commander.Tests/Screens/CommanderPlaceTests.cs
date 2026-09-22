using System;
using System.IO;
using Arlecchino.Commander.Tests.Support;
using Arlecchino.Commander.Views;
using Xunit;
using static Arlecchino.Commander.Localization;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
/// The disks of the machine as the list offers them. What is asserted is what a panel does with one,
/// since the disks a test host happens to be holding are nothing to hold the application to.
/// </summary>
public sealed class CommanderPlaceTests : IDisposable
{
    private readonly ScreenApp _app = new(ViewKind.Commander);

    public CommanderPlaceTests()
    {
        _app.Sessions.Start(_app.Folder, _app.Folder);
        _app.Settled();
    }

    public void Dispose() => _app.Dispose();

    private void Listing()
    {
        _app.Press(ConsoleKey.F1, control: true);
        _app.Settled();
    }

    /// <summary>
    /// Both what is mounted and what is only in the machine are listed, each saying what is on it and
    /// how large it is. A stick that was pushed in and mounted nowhere is the one worth listing.
    /// </summary>
    [Fact]
    public void WhatIsMountedAndWhatIsMerelyThereAreBothListed()
    {
        _app.Disks.Places.Add(new("Data", _app.Folder, "/dev/sdb1", "btrfs", 2000397795328, false));
        _app.Disks.Places.Add(new("ESD-USB", "", "/dev/sdd1", "vfat", 16063135744, true));

        Listing();

        var screen = _app.Frame();

        Assert.Contains("Data", screen, StringComparison.Ordinal);
        Assert.Contains("ESD-USB", screen, StringComparison.Ordinal);
        Assert.Contains("vfat · 15 GB · not mounted", screen, StringComparison.Ordinal);
    }

    /// <summary>A place that is already mounted is gone to without anything being asked of the machine.</summary>
    [Fact]
    public void AMountedPlaceIsGoneToAtOnce()
    {
        var folder = Directory.CreateTempSubdirectory("commander-mounted").FullName;

        try
        {
            _app.Disks.Places.Add(new("Data", folder, "/dev/sdb1", "btrfs", 1024, false));

            Listing();
            _app.Press(ConsoleKey.Enter);

            Assert.True(_app.Until(() => _app.Sessions.Left.Folder == folder));
            Assert.Empty(_app.Disks.MountedDevices);
        }
        finally
        {
            Directory.Delete(folder);
        }
    }

    /// <summary>
    /// Picking a place that is in the machine and mounted nowhere mounts it and goes there. Refusing to
    /// open it would leave the one row that is worth a key doing nothing at all.
    /// </summary>
    [Fact]
    public void APlaceThatIsNotMountedIsMountedAndGoneTo()
    {
        var folder = Directory.CreateTempSubdirectory("commander-mounting").FullName;

        try
        {
            _app.Disks.Landing = folder;
            _app.Disks.Places.Add(new("ESD-USB", "", "/dev/sdd1", "vfat", 1024, true));

            Listing();
            _app.Press(ConsoleKey.Enter);

            Assert.True(_app.Until(() => _app.Sessions.Left.Folder == folder));
            Assert.Equal("/dev/sdd1", Assert.Single(_app.Disks.MountedDevices));
            Assert.Equal(Loc(LocString.SaidMounted, "ESD-USB", folder), _app.State.Output);
        }
        finally
        {
            Directory.Delete(folder);
        }
    }

    /// <summary>A mounting the machine refused is said so rather than left as a panel that did not move.</summary>
    [Fact]
    public void AMountingThatWasRefusedIsSaidSo()
    {
        _app.Disks.Places.Add(new("ESD-USB", "", "/dev/sdd1", "vfat", 1024, true));

        Listing();
        _app.Press(ConsoleKey.Enter);

        Assert.True(_app.Until(() => _app.State.Output == Loc(LocString.SaidCouldNotMount, "ESD-USB")));
        Assert.Equal("/dev/sdd1", Assert.Single(_app.Disks.MountedDevices));
    }

    /// <summary>
    /// A machine that mounts nothing of its own says the place is not mounted and stops there. Windows
    /// and macOS answer only what they have already mounted, and there is nothing to ask them for.
    /// </summary>
    [Fact]
    public void AMachineThatMountsNothingSaysThePlaceIsNotMounted()
    {
        _app.Disks.CanMount = false;
        _app.Disks.Places.Add(new("ESD-USB", "", "/dev/sdd1", "vfat", 1024, true));

        Listing();
        _app.Press(ConsoleKey.Enter);
        _app.Settled();

        Assert.Equal(Loc(LocString.SaidPlaceNotMounted, "ESD-USB"), _app.State.Output);
        Assert.Empty(_app.Disks.MountedDevices);
    }
}
