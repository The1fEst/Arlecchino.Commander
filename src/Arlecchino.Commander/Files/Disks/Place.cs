namespace Arlecchino.Commander.Files.Disks;

/// <summary>
/// One place a panel could be sent: a volume that is mounted, or a partition in the machine that is
/// not. It is called by the label it carries rather than by the device it happens to be.
/// </summary>
/// <param name="Label">The volume label, or the device when it carries none.</param>
/// <param name="Path">Where it is mounted, or an empty string when it is not.</param>
/// <param name="Device">The device it is, as <c>/dev/sdb1</c>.</param>
/// <param name="Kind">The file system on it, as <c>btrfs</c> or <c>ntfs</c>.</param>
/// <param name="Size">How large it is, in bytes, or nought when nothing said.</param>
/// <param name="IsRemovable">Whether the drive it sits on can be taken out of the machine.</param>
public sealed record Place(
    string Label,
    string Path,
    string Device,
    string Kind,
    long Size,
    bool IsRemovable)
{
    /// <summary>Whether a panel could be sent here as it is, without mounting it first.</summary>
    public bool IsMounted => Path.Length > 0;
}
