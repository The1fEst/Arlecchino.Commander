using System;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Widgets;

namespace Arlecchino.Commander.Views;

/// <summary>
/// What one panel holds that the other does not hold the same of. The answer is given as marks rather
/// than as a report: what is left unmarked is what matches, and what is marked is already selected for
/// whichever of copy, move or delete the answer calls for.
/// </summary>
public static class Difference
{
    private const int SameSecond = 2;

    /// <summary>Marks, in both panels, everything the other one does not have the same of.</summary>
    /// <param name="left">One panel.</param>
    /// <param name="right">The other.</param>
    /// <returns>How much was marked, all told.</returns>
    public static int Mark(FilePanel left, FilePanel right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        left.State.Marks.Clear();
        right.State.Marks.Clear();

        return Odd(left, right) + Odd(right, left);
    }

    /// <summary>Marks what one panel has that the other has not, or has differently.</summary>
    /// <param name="panel">The panel being marked.</param>
    /// <param name="other">The one it is held against.</param>
    /// <returns>How many were marked.</returns>
    private static int Odd(FilePanel panel, FilePanel other)
    {
        var marked = 0;

        foreach (var entry in panel.Entries)
        {
            if (entry.IsParent || entry.IsFolder || Same(entry, Find(other, entry.Name)))
            {
                continue;
            }

            panel.State.Marks.Add(entry.Name);
            marked++;
        }

        return marked;
    }

    /// <summary>What the other panel calls by the same name.</summary>
    /// <param name="panel">The other panel.</param>
    /// <param name="name">The name to look for.</param>
    /// <returns>What it found, or nothing.</returns>
    private static FileEntry? Find(FilePanel panel, string name)
    {
        foreach (var entry in panel.Entries)
        {
            if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether two files are the same file. Two seconds of slack: a FAT volume keeps times to two
    /// seconds, and without the slack every file copied onto one would come back as differing.
    /// </summary>
    /// <param name="entry">One file.</param>
    /// <param name="other">The other, or nothing when there is none.</param>
    /// <returns><c>true</c> when they match.</returns>
    private static bool Same(FileEntry entry, FileEntry? other) =>
        other is not null &&
        entry.Size == other.Size &&
        Math.Abs((entry.Modified - other.Modified).TotalSeconds) < SameSecond;
}
