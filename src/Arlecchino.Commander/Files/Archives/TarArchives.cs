using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using Arlecchino.Commander.Files.Sources;

namespace Arlecchino.Commander.Files.Archives;

/// <summary>
/// A tar, packed with <c>gzip</c> or not, which the runtime reads itself. It has no table of contents:
/// what is in it is found by reading through it, and so is any one file taken out of it.
/// </summary>
public sealed class TarArchives : ArchiveFormat
{
    /// <summary>The one of these there is.</summary>
    public static readonly TarArchives Instance = new();

    /// <summary>The names this reads, the longest first so <c>.tar.gz</c> is not read as <c>.gz</c>.</summary>
    private static readonly string[] Endings = [".tar.gz", ".tgz", ".tar"];

    /// <summary>Whether a name is a tar this reads.</summary>
    /// <param name="name">The file name, in lower case.</param>
    /// <returns><c>true</c> when it is one.</returns>
    public static bool Names(string name)
    {
        foreach (var ending in Endings)
        {
            if (name.EndsWith(ending, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public override IReadOnlyList<ArchiveEntry> Read(string archive)
    {
        using var reader = Reading(archive);
        var entries = new List<ArchiveEntry>();

        while (reader.GetNextEntry(copyData: false) is { } entry)
        {
            entries.Add(new(
                entry.Name,
                entry.Length,
                entry.ModificationTime.LocalDateTime,
                entry.EntryType == TarEntryType.Directory));
        }

        return entries;
    }

    /// <summary>
    /// Reads through the archive until the entry turns up, and hands back its bytes with the reader
    /// still open behind them. A tar holds each file where it lies, so this is the only way to one.
    /// </summary>
    /// <param name="archive">The archive, as a path on this disk.</param>
    /// <param name="entry">The entry, as its path inside the archive.</param>
    /// <returns>The bytes of that entry.</returns>
    public override Stream Open(string archive, string entry)
    {
        var reader = Reading(archive);

        try
        {
            while (reader.GetNextEntry(copyData: false) is { } found)
            {
                if (Same(found.Name, entry) && found.DataStream is { } data)
                {
                    return new LeasedStream(data, reader);
                }
            }

            throw new FileNotFoundException(Loc(LocString.ArchiveNoEntry, entry), archive);
        }
        catch
        {
            reader.Dispose();

            throw;
        }
    }

    /// <summary>
    /// Opens the archive for reading through, unpacking it on the way when it was packed with
    /// <c>gzip</c>. Nothing here is left open on its own: closing the reader closes the file under it.
    /// </summary>
    /// <param name="archive">The archive, as a path on this disk.</param>
    /// <returns>The reader, which owns everything under it.</returns>
    private static TarReader Reading(string archive)
    {
        var file = File.OpenRead(archive);

        try
        {
            var name = Path.GetFileName(archive).ToLowerInvariant();
            var zipped = name.EndsWith(".gz", StringComparison.Ordinal) ||
                         name.EndsWith(".tgz", StringComparison.Ordinal);

            return new(zipped ? new GZipStream(file, CompressionMode.Decompress) : file);
        }
        catch
        {
            file.Dispose();

            throw;
        }
    }

    /// <summary>
    /// Whether an entry is the one being looked for, compared as the tree spells a path. A tar writes
    /// the leading <c>./</c> as often as not, and the tree is what the panel asked through.
    /// </summary>
    /// <param name="writtenPath">The path as the archive wrote it, which a tar keeps whole.</param>
    /// <param name="wantedPath">The path being looked for.</param>
    /// <returns><c>true</c> when they name the same entry.</returns>
    private static bool Same(string writtenPath, string wantedPath) => string.Equals(
        ArchiveTree.Tidied(writtenPath),
        ArchiveTree.Tidied(wantedPath),
        StringComparison.Ordinal);
}
