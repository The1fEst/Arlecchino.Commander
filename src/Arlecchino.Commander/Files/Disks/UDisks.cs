using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace Arlecchino.Commander.Files.Disks;

/// <summary>
/// What a Linux desktop asks about its disks, which is a service on the system bus rather than a file to
/// read. It answers the partitions that are in the machine as well as the ones that are mounted.
/// </summary>
public sealed class UDisks : Disks
{
    /// <summary>The name the service answers to on the system bus.</summary>
    public const string Service = "org.freedesktop.UDisks2";

    /// <summary>The object every block device and drive hangs under.</summary>
    public const string Root = "/org/freedesktop/UDisks2";

    /// <summary>The interface a partition carries, which says what it is and what is on it.</summary>
    public const string BlockInterface = "org.freedesktop.UDisks2.Block";

    /// <summary>The interface a partition carries once it holds a file system, saying where it is.</summary>
    public const string FilesystemInterface = "org.freedesktop.UDisks2.Filesystem";

    /// <summary>The interface the disk itself carries, which says whether it can be taken out.</summary>
    public const string DriveInterface = "org.freedesktop.UDisks2.Drive";

    private const string ObjectManager = "org.freedesktop.DBus.ObjectManager";

    /// <summary>The one of these there is.</summary>
    public static readonly UDisks Instance = new();

    /// <summary>
    /// Whether the service is on this machine at all, answered by looking for the file the bus would
    /// start it from. Opening the bus to ask would be a connection made for a question with no answer.
    /// </summary>
    public static bool Answers { get; } = Installed();

    /// <inheritdoc/>
    public override async Task<IReadOnlyList<Place>> ReadAsync()
    {
        try
        {
            using var connection = new DBusConnection(DBusAddress.System!);

            await connection.ConnectAsync().ConfigureAwait(false);

            return await connection
                .CallMethodAsync(Asking(connection), static (message, _) => Placed(message))
                .ConfigureAwait(false);
        }
        catch (Exception error) when (error is DBusExceptionBase or IOException or SocketException or
                                          ArgumentException)
        {
            return [];
        }
    }

    /// <summary>
    /// Reads what the service said into the places worth offering. The reply holds every object it knows
    /// about, of which a partition carrying a file system is the only kind a panel could be sent to.
    /// </summary>
    /// <param name="message">The reply, whose body is <c>a{oa{sa{sv}}}</c>.</param>
    /// <returns>The places, in the order the service listed them.</returns>
    private static List<Place> Placed(Message message)
    {
        var devices = new List<BlockDevice>();
        var removable = new Dictionary<string, bool>(StringComparer.Ordinal);
        var reader = message.GetBodyReader();
        var objects = reader.ReadDictionaryStart();

        while (reader.HasNext(objects))
        {
            var path = reader.ReadObjectPathAsString();
            var interfaces = reader.ReadDictionaryStart();
            var device = new BlockDevice();

            while (reader.HasNext(interfaces))
            {
                var name = reader.ReadString();
                var properties = reader.ReadDictionaryOfStringToVariantValue();

                switch (name)
                {
                    case BlockInterface:
                        device.Read(properties);

                        break;

                    case FilesystemInterface:
                        if (properties.TryGetValue("MountPoints", out var points) && points.Count > 0)
                        {
                            device.MountedAt(points.GetItem(0).GetArray<byte>());
                        }

                        break;

                    case DriveInterface:
                        removable[path] = BlockDevice.Bool(properties, "Removable");

                        break;
                }
            }

            if (device.IsFilesystem)
            {
                devices.Add(device);
            }
        }

        var places = new List<Place>(devices.Count);

        foreach (var device in devices)
        {
            places.Add(device.Place(removable.GetValueOrDefault(device.Drive)));
        }

        return places;
    }

    /// <summary>
    /// Writes the one question this asks. It stands apart from the asking because the writer is a
    /// borrowed buffer that cannot be held across a wait.
    /// </summary>
    /// <param name="connection">The bus the message is going out on.</param>
    /// <returns>The message, ready to send.</returns>
    private static MessageBuffer Asking(DBusConnection connection)
    {
        using var writer = connection.GetMessageWriter();

        writer.WriteMethodCallHeader(Service, Root, ObjectManager, "GetManagedObjects");

        return writer.CreateMessage();
    }

    /// <summary>
    /// Whether the service is installed, which is the file the bus would start it from. A machine
    /// without it is a container or a server, where the mounted volumes are the whole answer.
    /// </summary>
    /// <returns><c>true</c> when there is something to ask.</returns>
    private static bool Installed()
    {
        string[] places =
        [
            "/usr/share/dbus-1/system-services/org.freedesktop.UDisks2.service",
            "/usr/local/share/dbus-1/system-services/org.freedesktop.UDisks2.service",
        ];

        foreach (var path in places)
        {
            if (File.Exists(path))
            {
                return true;
            }
        }

        return false;
    }
}
