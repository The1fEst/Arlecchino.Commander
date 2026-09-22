using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Work;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Files.Archives;

/// <summary>
/// Which files are archives that can be walked into, and what reads each of them. Every one is read by
/// 7-Zip, so a machine without it walks into none of them.
/// </summary>
public static class Archives
{
    /// <summary>What reads an archive of this name, or nothing when it is not one this opens.</summary>
    /// <param name="path">The file, on this disk.</param>
    /// <returns>The format, or <c>null</c>.</returns>
    public static ArchiveFormat? For(string path)
    {
        if (!SevenZip.Works || !Names(path))
        {
            return null;
        }

        return PackedTarArchives.Names(Path.GetFileName(path).ToLowerInvariant())
            ? PackedTarArchives.Instance
            : SevenZipArchives.Instance;
    }

    /// <summary>
    /// Whether a file of this name is an archive, even on a machine without the program to read it. It is what tells a machine without 7-Zip which files it is missing it for.
    /// </summary>
    /// <param name="path">The file, on this disk.</param>
    /// <returns><c>true</c> when the name is an archive's.</returns>
    public static bool Names(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();

        return PackedTarArchives.Names(name) || SevenZipArchives.Names(name);
    }

    /// <summary>
    /// Makes an archive out of files on the disk. The program is run in the folder they are in, so it
    /// writes them under their own names rather than under the whole path each of them has.
    /// </summary>
    /// <param name="sources">What to put in it.</param>
    /// <param name="folder">The folder they are in.</param>
    /// <param name="archive">The archive to make, as a path on this disk.</param>
    /// <param name="outcome">What is being told how it went.</param>
    /// <param name="token">Gives up the work.</param>
    /// <returns>A task that finishes when the program has.</returns>
    public static Task PackAsync(
        IReadOnlyList<FileEntry> sources,
        string folder,
        string archive,
        Outcome outcome,
        CancellationToken token)
    {
        var names = new string[sources.Count];

        for (var at = 0; at < sources.Count; at++)
        {
            names[at] = sources[at].Name;
        }

        return SevenZipWriting.AddAsync(archive, folder, names, outcome, token);
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
    public static ArchiveSource? Open(string path) => For(path) is { } format ? Open(path, format) : null;

    /// <summary>
    /// What reads a file as an archive whatever it is called. 7-Zip tells a format by what is in the
    /// file, so a name that says nothing is handed to it as a single-layer archive.
    /// </summary>
    /// <param name="path">The file, on this disk.</param>
    /// <returns>The format to try.</returns>
    public static ArchiveFormat Fallback(string path) => For(path) ?? SevenZipArchives.Instance;

    /// <summary>Opens an archive with a format already chosen, or hands back nothing.</summary>
    /// <param name="path">The file, on this disk.</param>
    /// <param name="format">What reads it.</param>
    /// <returns>The source to connect a panel to, or <c>null</c>.</returns>
    public static ArchiveSource? Open(string path, ArchiveFormat format)
    {
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
