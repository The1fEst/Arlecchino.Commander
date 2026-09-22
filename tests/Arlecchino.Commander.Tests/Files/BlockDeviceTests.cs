using System.Collections.Generic;
using System.Text;
using Arlecchino.Commander.Files.Disks;
using Tmds.DBus.Protocol;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// What udisks2 says about a partition, turned into the row a panel is sent from. The properties are
/// written here as the service writes them, a device path being bytes with a nought on the end.
/// </summary>
public sealed class BlockDeviceTests
{
    private static Dictionary<string, VariantValue> Partition(
        string label = "Data",
        string device = "/dev/sdb1",
        string kind = "btrfs",
        string usage = "filesystem",
        ulong size = 2000397795328,
        bool ignored = false) =>
        new()
        {
            ["IdLabel"] = VariantValue.String(label),
            ["Device"] = VariantValue.Array(Terminated(device)),
            ["IdType"] = VariantValue.String(kind),
            ["IdUsage"] = VariantValue.String(usage),
            ["HintIgnore"] = VariantValue.Bool(ignored),
            ["Size"] = VariantValue.UInt64(size),
            ["Drive"] = VariantValue.ObjectPath(new("/org/freedesktop/UDisks2/drives/Kingston")),
        };

    private static byte[] Terminated(string path) => [.. Encoding.UTF8.GetBytes(path), 0];

    [Fact]
    public void AMountedPartitionIsOfferedUnderItsLabel()
    {
        var device = new BlockDevice();

        device.Read(Partition());
        device.MountedAt(Terminated("/home/fest/Disks/Data"));

        var place = device.Place(removable: false);

        Assert.True(device.IsFilesystem);
        Assert.Equal("Data", place.Label);
        Assert.Equal("/home/fest/Disks/Data", place.Path);
        Assert.Equal("/dev/sdb1", place.Device);
        Assert.Equal("btrfs", place.Kind);
        Assert.Equal(2000397795328, place.Size);
        Assert.True(place.IsMounted);
    }

    /// <summary>
    /// A partition that was never given a name is called by the device it is, which is what a shell
    /// would call it. A row with nothing in the name column says less than the device does.
    /// </summary>
    [Fact]
    public void APartitionWithNoLabelIsCalledByItsDevice()
    {
        var device = new BlockDevice();

        device.Read(Partition(label: ""));

        Assert.Equal("/dev/sdb1", device.Place(removable: false).Label);
    }

    /// <summary>
    /// A partition in the machine and mounted nowhere is still offered, since offering it is the only
    /// way to say that the stick that was just pushed in is there.
    /// </summary>
    [Fact]
    public void APartitionThatIsNotMountedIsStillOffered()
    {
        var device = new BlockDevice();

        device.Read(Partition(label: "ESD-USB", device: "/dev/sdd1", kind: "vfat"));

        var place = device.Place(removable: true);

        Assert.True(device.IsFilesystem);
        Assert.False(place.IsMounted);
        Assert.Equal("", place.Path);
        Assert.True(place.IsRemovable);
    }

    /// <summary>
    /// What the service itself says to keep back is kept back. The boot partition is mounted, holds a
    /// file system and is no place anyone browses, and the service is the one that knows it.
    /// </summary>
    [Fact]
    public void WhatTheServiceSaysToIgnoreIsNotOffered()
    {
        var device = new BlockDevice();

        device.Read(Partition(ignored: true));
        device.MountedAt(Terminated("/boot"));

        Assert.False(device.IsFilesystem);
    }

    /// <summary>A partition with no file system on it is nowhere to send a panel.</summary>
    [Fact]
    public void APartitionWithNoFilesystemIsNotOffered()
    {
        var device = new BlockDevice();

        device.Read(Partition(usage: "raw"));

        Assert.False(device.IsFilesystem);
    }

    /// <summary>
    /// The nought the service ends a path with is cut off. Left on, every path would end in a byte no
    /// file system call takes, and no place at all could be opened.
    /// </summary>
    [Fact]
    public void ThePathIsReadWithoutTheNoughtTheServiceEndsItWith()
    {
        var device = new BlockDevice();

        device.Read(Partition());
        device.MountedAt(Terminated("/media/stick"));

        Assert.Equal("/media/stick", device.Place(removable: false).Path);
    }
}
