using System;
using System.Linq;

namespace Arlecchino.Commander.Model;

/// <summary>
/// What a paste amounts to on a row that holds one line. The clipboard carries whatever was last copied
/// anywhere on the machine, and the rows here have one line to put it on.
/// </summary>
internal static class Pasted
{
    /// <summary>
    /// The first line of what was pasted, with the control characters left out. The rest is dropped rather
    /// than joined up, since a newline in the middle of a paste would read as the Enter key.
    /// </summary>
    /// <param name="text">What was pasted.</param>
    /// <returns>What a line of one row can take of it.</returns>
    public static string OneLine(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var end = text.IndexOfAny(['\r', '\n']);
        var first = end < 0 ? text : text[..end];

        return string.Concat(first.Where(static character => !char.IsControl(character)));
    }
}
