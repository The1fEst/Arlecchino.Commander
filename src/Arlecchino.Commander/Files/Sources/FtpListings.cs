using System;
using System.Collections.Generic;
using System.Globalization;

namespace Arlecchino.Commander.Files.Sources;

/// <summary>
///     Reads what a server said. Everything here is a pure reading of text, which is where the whole of the
///     guesswork in FTP lives. A listing has two shapes and a half, and the two ways of asking for a data
///     connection answer with the port buried differently.
/// </summary>
public static class FtpListings
{
    /// <summary>
    ///     Reads an <c>MLSD</c> listing, which is the one worth having: every entry is
    ///     <c>fact=value;…; name</c>, the facts are named, and the time is written the same way by
    ///     everyone. Entries for the folder itself and its parent are left out.
    /// </summary>
    /// <param name="text">What came over the data connection.</param>
    /// <returns>The entries.</returns>
    public static IReadOnlyList<FtpEntry> Machine(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var entries = new List<FtpEntry>();

        foreach (var line in Lines(text))
        {
            var split = line.IndexOf(' ', StringComparison.Ordinal);

            if (split <= 0 || split == line.Length - 1)
            {
                continue;
            }

            var name = line[(split + 1)..];
            var kind = "";
            var size = 0L;
            var mode = 0;
            var modified = DateTime.MinValue;

            foreach (var fact in line[..split].Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var at = fact.IndexOf('=', StringComparison.Ordinal);

                if (at <= 0)
                {
                    continue;
                }

                var named = fact[..at];
                var value = fact[(at + 1)..];

                if (named.Equals("type", StringComparison.OrdinalIgnoreCase))
                {
                    kind = value;
                }
                else if (named.Equals("size", StringComparison.OrdinalIgnoreCase))
                {
                    _ = long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out size);
                }
                else if (named.Equals("modify", StringComparison.OrdinalIgnoreCase))
                {
                    modified = Stamp(value);
                }
                else if (named.Equals("UNIX.mode", StringComparison.OrdinalIgnoreCase))
                {
                    mode = Octal(value);
                }
            }

            if (kind.Equals("cdir", StringComparison.OrdinalIgnoreCase) ||
                kind.Equals("pdir", StringComparison.OrdinalIgnoreCase) ||
                name is "." or "..")
            {
                continue;
            }

            var folder = kind.Equals("dir", StringComparison.OrdinalIgnoreCase);

            entries.Add(new(name, folder, folder ? 0 : size, modified, mode));
        }

        return entries;
    }

    /// <summary>
    ///     Reads a <c>LIST</c> listing, which is whatever the server felt like printing. Two shapes cover
    ///     nearly everything: the Unix one that <c>ls -l</c> writes, and the one old Windows servers write.
    ///     Anything else is skipped rather than guessed at.
    /// </summary>
    /// <param name="text">What came over the data connection.</param>
    /// <returns>The entries it could read.</returns>
    public static IReadOnlyList<FtpEntry> Plain(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var entries = new List<FtpEntry>();

        foreach (var line in Lines(text))
        {
            var read = Unix(line) ?? Dos(line);

            if (read is { Name: not ("." or ".." or "") } entry)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    /// <summary>
    ///     Finds the port in the answer to <c>EPSV</c>, which writes it between three delimiters of its own
    ///     choosing: <c>229 Entering Extended Passive Mode (|||6446|)</c>.
    /// </summary>
    /// <param name="text">The reply.</param>
    /// <returns>The port, or nought when it is not in there.</returns>
    public static int ExtendedPort(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var open = text.IndexOf('(', StringComparison.Ordinal);
        var close = text.IndexOf(')', StringComparison.Ordinal);

        if (open < 0 || close <= open + 1)
        {
            return 0;
        }

        var inside = text[(open + 1)..close];
        var mark = inside[0];
        var parts = inside.Split(mark, StringSplitOptions.RemoveEmptyEntries);

        return parts.Length == 0 ? 0 : Port(parts[^1]);
    }

    /// <summary>
    ///     Finds the port in the answer to <c>PASV</c>, where it is the last two of six numbers, high byte
    ///     first: <c>227 Entering Passive Mode (10,0,0,1,25,14)</c> means 25 × 256 + 14.
    /// </summary>
    /// <param name="text">The reply.</param>
    /// <returns>The port, or nought when it is not in there.</returns>
    public static int PassivePort(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var open = text.IndexOf('(', StringComparison.Ordinal);
        var close = text.IndexOf(')', StringComparison.Ordinal);

        if (open < 0 || close <= open)
        {
            return 0;
        }

        var numbers = text[(open + 1)..close].Split(',');

        if (numbers.Length < 6)
        {
            return 0;
        }

        var high = Port(numbers[^2]);
        var low = Port(numbers[^1]);

        return high < 0 || low < 0 || high > 255 || low > 255 ? 0 : high * 256 + low;
    }

    private static FtpEntry? Unix(string line)
    {
        if (line.Length < 10 || line[0] is not ('d' or '-' or 'l'))
        {
            return null;
        }

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 9)
        {
            return null;
        }

        var name = Tail(line, parts, 8);

        if (line[0] == 'l' && name.IndexOf(" -> ", StringComparison.Ordinal) is var arrow and > 0)
        {
            name = name[..arrow];
        }

        _ = long.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var size);

        return new(
            name,
            line[0] == 'd',
            line[0] == 'd' ? 0 : size,
            Listed(parts[5], parts[6], parts[7]),
            Permissions(line));
    }

    /// <summary>
    ///     Reads the date <c>ls -l</c> writes, which is three words and a decision. Something changed
    ///     within the last half year is written with the time and no year at all — so the year is the one
    ///     that puts it in the past, since a listing cannot hold tomorrow.
    /// </summary>
    /// <param name="month">The month, as three letters.</param>
    /// <param name="day">The day.</param>
    /// <param name="last">Either the time or the year.</param>
    /// <returns>When it changed, or the default when the three do not read as a date.</returns>
    private static DateTime Listed(string month, string day, string last)
    {
        if (!last.Contains(':', StringComparison.Ordinal))
        {
            return DateTime.TryParseExact(
                $"{month} {day} {last}",
                ["MMM d yyyy", "MMM dd yyyy"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var older)
                ? older
                : default;
        }

        if (!DateTime.TryParseExact(
                $"{month} {day} {last}",
                ["MMM d HH:mm", "MMM dd HH:mm"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var recent))
        {
            return default;
        }

        var now = DateTime.Now;
        var dated = new DateTime(now.Year, recent.Month, recent.Day, recent.Hour, recent.Minute, 0);

        return dated > now.AddDays(1) ? dated.AddYears(-1) : dated;
    }

    private static FtpEntry? Dos(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 4 || !parts[0].Contains('-', StringComparison.Ordinal))
        {
            return null;
        }

        var folder = parts[2].Equals("<DIR>", StringComparison.OrdinalIgnoreCase);
        var name = Tail(line, parts, 3);

        _ = long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var size);

        return new(name, folder, folder ? 0 : size, Dated(parts[0], parts[1]), 0);
    }

    /// <summary>
    ///     Everything from the given word to the end of the line, taken from the line itself rather than
    ///     from the split, so that a name holding spaces survives.
    /// </summary>
    /// <param name="line">The whole line.</param>
    /// <param name="parts">It, split on spaces.</param>
    /// <param name="from">Which word the name starts at.</param>
    /// <returns>The name.</returns>
    private static string Tail(string line, string[] parts, int from)
    {
        var at = 0;

        for (var word = 0; word < from; word++)
        {
            at = line.IndexOf(parts[word], at, StringComparison.Ordinal) + parts[word].Length;
        }

        return line[at..].TrimStart();
    }

    private static int Permissions(string line)
    {
        if (line.Length < 10)
        {
            return 0;
        }

        var digits = 0;

        for (var group = 0; group < 3; group++)
        {
            var at = 1 + group * 3;
            var value = (line[at] == 'r' ? 4 : 0) + (line[at + 1] == 'w' ? 2 : 0) + (line[at + 2] is 'x' or 's' ? 1 : 0);

            digits = digits * 10 + value;
        }

        return digits;
    }

    private static DateTime Stamp(string value)
    {
        return DateTime.TryParseExact(
            value.Length > 14 ? value[..14] : value,
            "yyyyMMddHHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var stamp)
            ? stamp.ToLocalTime()
            : default;
    }

    private static DateTime Dated(string date, string time)
    {
        return DateTime.TryParse($"{date} {time}", CultureInfo.InvariantCulture, DateTimeStyles.None, out var stamp)
            ? stamp
            : default;
    }

    private static int Octal(string value)
    {
        var digits = 0;

        foreach (var letter in value)
        {
            if (letter is < '0' or > '7')
            {
                return 0;
            }

            digits = digits * 10 + (letter - '0');
        }

        return digits % 1000;
    }

    private static int Port(string value)
    {
        return int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var port) ? port : -1;
    }

    private static IEnumerable<string> Lines(string text)
    {
        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            var trimmed = line.TrimEnd();
            if (trimmed.Length > 0)
            {
                yield return trimmed;
            }
        }
    }
}
