using System;
using System.Collections.Generic;
using System.Globalization;

namespace Arlecchino.Commander.Files.Archives;

/// <summary>
/// What <c>7z l -slt</c> writes, read back as entries. It is a block of named values for each entry,
/// one value to a line, which is the one shape the program writes that was meant to be read again.
/// </summary>
public static class SevenZipListing
{
    /// <summary>The line the header ends on, after which the blocks begin.</summary>
    private const string Header = "----------";

    /// <summary>Reads a whole listing.</summary>
    /// <param name="listing">Everything the program wrote.</param>
    /// <returns>The entries, in the order they were listed.</returns>
    public static IReadOnlyList<ArchiveEntry> Read(string listing)
    {
        var entries = new List<ArchiveEntry>();
        var lines = listing.ReplaceLineEndings("\n").Split('\n');
        var started = false;
        var block = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in lines)
        {
            if (!started)
            {
                started = line.StartsWith(Header, StringComparison.Ordinal);

                continue;
            }

            if (line.Length == 0)
            {
                Take(entries, block);
                block.Clear();

                continue;
            }

            var (name, value) = Split(line);

            if (name.Length > 0)
            {
                block[name] = value;
            }
        }

        Take(entries, block);

        return entries;
    }

    /// <summary>
    /// One block as an entry, when it holds a path. A block with nothing in it is the gap between two
    /// of them, and the last block has no gap after it.
    /// </summary>
    /// <param name="entries">Where it goes.</param>
    /// <param name="block">The values read so far.</param>
    private static void Take(List<ArchiveEntry> entries, Dictionary<string, string> block)
    {
        if (!block.TryGetValue("Path", out var path) || path.Length == 0)
        {
            return;
        }

        entries.Add(new(path, Size(block), Modified(block), IsFolder(block)));
    }

    /// <summary>
    /// A line as the name it sets and the value it sets it to. The first <c>=</c> is the one that
    /// divides them, since a path is free to hold one and the names never do.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The name and the value, or two empty strings when the line sets nothing.</returns>
    private static (string Name, string Value) Split(string line)
    {
        var cut = line.IndexOf(" = ", StringComparison.Ordinal);

        return cut < 0 ? ("", "") : (line[..cut], line[(cut + 3)..]);
    }

    private static long Size(Dictionary<string, string> block) =>
        block.TryGetValue("Size", out var size) &&
        long.TryParse(size, CultureInfo.InvariantCulture, out var bytes)
            ? bytes
            : 0;

    /// <summary>
    /// Whether the entry is a folder, which the program says in the first letter of its attributes.
    /// The rest of them are the mode, which a panel over an archive has nothing to do with.
    /// </summary>
    /// <param name="block">The values of the block.</param>
    /// <returns><c>true</c> when it is a folder.</returns>
    private static bool IsFolder(Dictionary<string, string> block) =>
        block.TryGetValue("Attributes", out var attributes) &&
        attributes.StartsWith('D');

    /// <summary>
    /// When the entry was written, which the program writes as a local time with no zone on it. An
    /// entry whose time it never learned is left blank rather than written as a nought.
    /// </summary>
    /// <param name="block">The values of the block.</param>
    /// <returns>When it was written, or nothing at all.</returns>
    private static DateTime Modified(Dictionary<string, string> block) =>
        block.TryGetValue("Modified", out var written) &&
        DateTime.TryParse(written, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment)
            ? moment
            : default;
}
