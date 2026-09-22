using System;
using System.IO;

namespace Arlecchino.Commander.Files.Archives;

/// <summary>
/// Which files are archives that can be walked into, and what reads each of them. The document formats
/// that are zips underneath are left out, since what anyone means by opening one is the document.
/// </summary>
public static class Archives
{
    /// <summary>What reads an archive of this name, or nothing when it is not one this opens.</summary>
    /// <param name="path">The file, on this disk.</param>
    /// <returns>The format, or <c>null</c>.</returns>
    public static ArchiveFormat? For(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();

        if (TarArchives.Names(name))
        {
            return TarArchives.Instance;
        }

        if (SevenZip.Works && PackedTarArchives.Names(name))
        {
            return PackedTarArchives.Instance;
        }

        if (SevenZip.Works && SevenZipArchives.Names(name))
        {
            return SevenZipArchives.Instance;
        }

        return Path.GetExtension(name) switch
        {
            ".zip" or ".jar" or ".war" or ".nupkg" or ".whl" or ".appx" or ".msix" or ".vsix" or ".xpi" or
                ".crx" => ZipArchives.Instance,
            _ => null,
        };
    }

    /// <summary>
    /// Whether an archive of this name can be written into. The program rewrites these three in place;
    /// a format packed inside another it refuses outright, and the rest it only ever reads.
    /// </summary>
    /// <param name="path">The file, on this disk.</param>
    /// <returns><c>true</c> when files can be put in, taken out and renamed.</returns>
    public static bool Writable(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".zip" or ".jar" or ".war" or ".nupkg" or ".whl" or ".appx" or ".msix" or ".vsix" or ".xpi" or
                ".crx" or ".7z" or ".tar" => true,
            _ => false,
        };

    /// <summary>
    /// Opens an archive, or hands back nothing when it will not open. A file that names itself an
    /// archive and is not one, or is one that was cut off in the middle, is not worth a dialog.
    /// </summary>
    /// <param name="path">The file, on this disk.</param>
    /// <returns>The source to connect a panel to, or <c>null</c>.</returns>
    public static ArchiveSource? Open(string path)
    {
        if (For(path) is not { } format)
        {
            return null;
        }

        try
        {
            return new(path, format);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or
                                          UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }
}
