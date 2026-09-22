using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Files.Disks;

/// <summary>
/// What the machine has to put a panel on: the volumes it is holding, and on Linux the partitions it is
/// holding that have not been mounted. Asking waits, since on Linux the answer comes off a bus.
/// </summary>
public abstract class Disks
{
    /// <summary>The one this machine has.</summary>
    public static Disks Current { get; } = Pick();

    /// <summary>Whether a place that is not mounted could be mounted from here.</summary>
    public abstract bool Mounts { get; }

    /// <summary>Every place a panel could be sent, in the order they are offered.</summary>
    /// <returns>The places, or nothing at all when the machine would not say.</returns>
    public abstract Task<IReadOnlyList<Place>> ReadAsync();

    /// <summary>Mounts a place that is in the machine and is not mounted.</summary>
    /// <param name="place">Which place.</param>
    /// <returns>Where it landed, or an empty string when it was refused.</returns>
    public abstract Task<string> TryMountAsync(Place place);

    /// <summary>
    /// How much room is left on a place, asked of the mount point itself. A path under it would answer
    /// for whichever volume the path is rooted on, which on Unix is the one volume every path is rooted on.
    /// </summary>
    /// <param name="place">The place, which has to be mounted to have an answer.</param>
    /// <returns>The room in words, or an empty string when it could not be had.</returns>
    public static string Room(Place place)
    {
        if (!place.IsMounted)
        {
            return "";
        }

        try
        {
            return Sizes.Free(new DriveInfo(place.Path).AvailableFreeSpace);
        }
        catch (Exception error) when (error is ArgumentException or IOException or
                                          UnauthorizedAccessException or DriveNotFoundException)
        {
            return "";
        }
    }

    private static Disks Pick() =>
        OperatingSystem.IsLinux() && UDisks.Answers ? UDisks.Instance : MountedDisks.Instance;
}

/// <summary>
/// The volumes that are mounted and nothing else, which is as much as Windows and macOS have to say and
/// as much as a Linux machine running no udisks2 will say. A partition not mounted is not offered.
/// </summary>
public sealed class MountedDisks : Disks
{
    /// <summary>The one of these there is.</summary>
    public static readonly MountedDisks Instance = new();

    /// <inheritdoc/>
    public override bool Mounts => false;

    /// <inheritdoc/>
    public override Task<string> TryMountAsync(Place place) => Task.FromResult("");

    /// <inheritdoc/>
    public override Task<IReadOnlyList<Place>> ReadAsync()
    {
        var places = new List<Place>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (Offered(drive) is { } place)
            {
                places.Add(place);
            }
        }

        return Task.FromResult<IReadOnlyList<Place>>(places);
    }

    /// <summary>
    /// What one mounted volume comes to, or nothing when it is not one worth offering. Unix answers
    /// every kernel file system here, and none of them is a place anyone means to browse.
    /// </summary>
    /// <param name="drive">What the runtime said is mounted.</param>
    /// <returns>The place, or <c>null</c>.</returns>
    private static Place? Offered(DriveInfo drive)
    {
        try
        {
            if (!drive.IsReady || Kernel(drive.DriveFormat))
            {
                return null;
            }

            var root = drive.RootDirectory.FullName;
            var label = drive.VolumeLabel is { Length: > 0 } named ? named : root;

            return new(
                label,
                root,
                drive.Name,
                drive.DriveFormat,
                drive.TotalSize,
                drive.DriveType == DriveType.Removable);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                                          DriveNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether the kernel keeps this file system for itself. Unix mounts a dozen of them under a
    /// running machine, and a list of drives holding <c>proc</c> and <c>sysfs</c> is a list left unread.
    /// </summary>
    /// <param name="format">What the file system is called.</param>
    /// <returns><c>true</c> when it is not a place.</returns>
    private static bool Kernel(string format) => format is
        "proc" or "sysfs" or "devtmpfs" or "tmpfs" or "devpts" or "cgroup" or "cgroup2" or "securityfs" or
        "debugfs" or "tracefs" or "pstore" or "bpf" or "configfs" or "fusectl" or "hugetlbfs" or "mqueue" or
        "autofs" or "binfmt_misc" or "efivarfs" or "ramfs" or "nsfs" or "squashfs" or "overlay";
}
