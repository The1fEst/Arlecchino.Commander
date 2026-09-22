using System.Collections.Generic;
using System.Text;
using Tmds.DBus.Protocol;

namespace Arlecchino.Commander.Files.Disks;

/// <summary>
/// One block device as udisks2 describes it, filled in from whichever of its interfaces turn up. It is
/// a shape of its own because the interfaces arrive one after another under one object path.
/// </summary>
internal sealed class BlockDevice
{
    private string _label = "";
    private string _device = "";
    private string _kind = "";
    private string _usage = "";
    private string _mountPoint = "";
    private long _size;
    private bool _ignored;

    /// <summary>The disk this partition sits on, which is what says whether it can be taken out.</summary>
    public string Drive { get; private set; } = "";

    /// <summary>Whether this is a partition holding a file system, and one worth offering.</summary>
    public bool IsFilesystem => !_ignored && _usage == "filesystem" && _device.Length > 0;

    /// <summary>Whether a property of a set is there and is true.</summary>
    /// <param name="properties">The properties to look in.</param>
    /// <param name="name">Which property.</param>
    /// <returns><c>true</c> when it is there and is set.</returns>
    public static bool Bool(Dictionary<string, VariantValue> properties, string name) =>
        properties.TryGetValue(name, out var value) && value.Type == VariantValueType.Bool && value.GetBool();

    /// <summary>Takes what the block interface says about it.</summary>
    /// <param name="properties">The properties of that interface.</param>
    public void Read(Dictionary<string, VariantValue> properties)
    {
        _label = Text(properties, "IdLabel");
        _device = properties.TryGetValue("Device", out var device) &&
                  device is { Type: VariantValueType.Array, ItemType: VariantValueType.Byte }
            ? Terminated(device.GetArray<byte>())
            : "";
        _kind = Text(properties, "IdType");
        _usage = Text(properties, "IdUsage");
        _ignored = Bool(properties, "HintIgnore");
        _size = properties.TryGetValue("Size", out var size) && size.Type == VariantValueType.UInt64
            ? (long)size.GetUInt64()
            : 0;

        Drive = properties.TryGetValue("Drive", out var drive) && drive.Type == VariantValueType.ObjectPath
            ? drive.GetObjectPathAsString()
            : "";
    }

    /// <summary>Takes where the file system interface says it is mounted.</summary>
    /// <param name="path">The mount point, as the bytes the service wrote it in.</param>
    public void MountedAt(byte[] path) => _mountPoint = Terminated(path);

    /// <summary>What this block device comes to as a place.</summary>
    /// <param name="removable">Whether the disk it sits on can be taken out of the machine.</param>
    /// <returns>The place.</returns>
    public Place Place(bool removable) =>
        new(_label.Length > 0 ? _label : _device, _mountPoint, _device, _kind, _size, removable);

    private static string Text(Dictionary<string, VariantValue> properties, string name) =>
        properties.TryGetValue(name, out var value) && value.Type == VariantValueType.String
            ? value.GetString()
            : "";

    /// <summary>
    /// A path the service writes as bytes rather than as a string, which is how it carries a name the
    /// kernel took and promised no encoding for. The last byte of it is a nought.
    /// </summary>
    /// <param name="bytes">The path as it came over.</param>
    /// <returns>The path, without the nought the service ends it with.</returns>
    private static string Terminated(byte[] bytes)
    {
        var length = bytes.Length;

        while (length > 0 && bytes[length - 1] == 0)
        {
            length--;
        }

        return Encoding.UTF8.GetString(bytes, 0, length);
    }
}
