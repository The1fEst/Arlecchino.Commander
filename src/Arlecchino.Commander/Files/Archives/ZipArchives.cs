using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Arlecchino.Commander.Files.Sources;

namespace Arlecchino.Commander.Files.Archives;

/// <summary>
/// A zip, which the runtime reads itself. Its table of contents sits at the end of the file, so one
/// entry is opened without reading the ones before it.
/// </summary>
public sealed class ZipArchives : ArchiveFormat
{
    /// <summary>The one of these there is.</summary>
    public static readonly ZipArchives Instance = new();

    /// <inheritdoc/>
    public override IReadOnlyList<ArchiveEntry> Read(string archive)
    {
        using var zip = ZipFile.OpenRead(archive);
        var entries = new List<ArchiveEntry>(zip.Entries.Count);

        foreach (var entry in zip.Entries)
        {
            entries.Add(new(
                entry.FullName,
                entry.Length,
                Written(entry),
                entry.FullName.EndsWith('/')));
        }

        return entries;
    }

    /// <summary>
    /// Opens one entry, holding the archive open behind it. The stream that comes back is unpacking as
    /// it is read, so the archive has to outlive it rather than be closed on the way out of here.
    /// </summary>
    /// <param name="archive">The archive, as a path on this disk.</param>
    /// <param name="entry">The entry, as its path inside the archive.</param>
    /// <returns>The bytes of that entry.</returns>
    public override Stream Open(string archive, string entry)
    {
        var zip = ZipFile.OpenRead(archive);

        try
        {
            if (zip.GetEntry(entry) is not { } found)
            {
                throw new FileNotFoundException(Loc(LocString.ArchiveNoEntry, entry), archive);
            }

            return new LeasedStream(found.Open(), zip);
        }
        catch
        {
            zip.Dispose();

            throw;
        }
    }

    /// <summary>
    /// When an entry was written. A zip records a local time with no zone on it, and one written before
    /// 1980 is out of the range the format can hold, which the runtime reports by refusing the read.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>When it was written, or nothing at all.</returns>
    private static DateTime Written(ZipArchiveEntry entry)
    {
        try
        {
            return entry.LastWriteTime.DateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return default;
        }
    }
}
