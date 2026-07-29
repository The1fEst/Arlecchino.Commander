using System;
using System.IO;
using Renci.SshNet.Sftp;

namespace Arlecchino.Commander.Files;

public static class RemotePaths
{
    public const string Root = "/";

    public static string Combine(string folder, string name) =>
        folder.EndsWith('/') ? folder + name : $"{folder}/{name}";

    public static string? Parent(string folder)
    {
        var trimmed = folder.TrimEnd('/');

        if (trimmed.Length == 0)
        {
            return null;
        }

        var cut = trimmed.LastIndexOf('/');

        return cut switch
        {
            < 0 => null,
            0 => Root,
            _ => trimmed[..cut],
        };
    }

    public static string NameOf(string path)
    {
        var trimmed = path.TrimEnd('/');
        var cut = trimmed.LastIndexOf('/');

        return cut < 0 ? trimmed : trimmed[(cut + 1)..];
    }

    public static bool IsHidden(string name) => name.StartsWith('.');

    /// <summary>
    /// The permission bits of an SFTP entry as one number. The protocol reports them as nine flags,
    /// which is the same thing a chmod writes in three digits.
    /// </summary>
    /// <param name="attributes">What the server said about the entry.</param>
    /// <returns>The number.</returns>
    public static int ModeOf(SftpFileAttributes attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var mode = 0;

        mode |= attributes.OwnerCanRead ? 256 : 0;
        mode |= attributes.OwnerCanWrite ? 128 : 0;
        mode |= attributes.OwnerCanExecute ? 64 : 0;
        mode |= attributes.GroupCanRead ? 32 : 0;
        mode |= attributes.GroupCanWrite ? 16 : 0;
        mode |= attributes.GroupCanExecute ? 8 : 0;
        mode |= attributes.OthersCanRead ? 4 : 0;
        mode |= attributes.OthersCanWrite ? 2 : 0;
        mode |= attributes.OthersCanExecute ? 1 : 0;

        return mode;
    }

    public static IOException AsIoException(Exception error) => new(error.Message, error);
}
