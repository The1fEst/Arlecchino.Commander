using System;

namespace Arlecchino.Commander.Files.Archives;

/// <summary>
/// How far the program has got, read off what it writes while it works. It draws over one line with
/// carriage returns and backspaces rather than writing a line per step, so the line is rebuilt first.
/// </summary>
public static class SevenZipProgress
{
    /// <summary>
    /// The letters the program puts between the count and the name, one for what it is doing to the
    /// file. Adding, updating and keeping each have a letter of their own, and none of them is a name.
    /// </summary>
    private const string Doings = "+U=TA.";

    /// <summary>One line it drew, as how far along it is and what it is on.</summary>
    /// <param name="line">The line, with the backspaces already taken out of it.</param>
    /// <returns>The step, or <c>null</c> when the line was not one of progress.</returns>
    public static (int Percent, string Name)? Step(string line)
    {
        var text = line.Trim();
        var cut = text.IndexOf('%', StringComparison.Ordinal);

        if (cut <= 0 || !int.TryParse(text[..cut], out var percent) || percent is < 0 or > 100)
        {
            return null;
        }

        return (percent, Named(text[(cut + 1)..]));
    }

    /// <summary>
    /// The file the program named after the percentage, when it named one. What stands between them is
    /// a count of the files done and a letter for what is being done to this one.
    /// </summary>
    /// <param name="rest">Whatever followed the percentage.</param>
    /// <returns>The name, or an empty string when it named none.</returns>
    private static string Named(string rest)
    {
        for (var at = 0; at < rest.Length - 1; at++)
        {
            if (rest[at] is not ' ' || Doings.IndexOf(rest[at + 1]) < 0)
            {
                continue;
            }

            if (at + 2 < rest.Length && rest[at + 2] == ' ')
            {
                return rest[(at + 3)..].Trim();
            }
        }

        return "";
    }
}
