using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Arlecchino.Commander.Model;

/// <summary>
/// Shell patterns, the kind that goes into the select-group box: <c>*</c> for any run of characters,
/// <c>?</c> for one, and a comma for another pattern beside it.
/// </summary>
public static class Glob
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(1);

    /// <summary>
    /// What a name typed with no wildcards stands for: that much of a name, matched anywhere in it. A
    /// pattern that already carries wildcards is left as it was.
    /// </summary>
    /// <param name="pattern">What was typed.</param>
    /// <returns>The pattern to match names against, which is everything when nothing was typed.</returns>
    public static string Anywhere(string pattern)
    {
        var pieces = pattern.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (pieces.Length == 0)
        {
            return "*";
        }

        for (var at = 0; at < pieces.Length; at++)
        {
            if (!pieces[at].Contains('*', StringComparison.Ordinal) &&
                !pieces[at].Contains('?', StringComparison.Ordinal))
            {
                pieces[at] = $"*{pieces[at]}*";
            }
        }

        return string.Join(',', pieces);
    }

    /// <summary>Whether a name fits a pattern.</summary>
    /// <param name="name">The name.</param>
    /// <param name="pattern">The pattern, which may be several separated by commas.</param>
    /// <returns><c>true</c> when any one of them fits.</returns>
    public static bool Matches(string name, string pattern)
    {
        foreach (var piece in pattern.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Regex.IsMatch(name, Translate(piece), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Patience))
            {
                return true;
            }
        }

        return false;
    }

    private static string Translate(string pattern)
    {
        var built = new StringBuilder("^");

        foreach (var character in pattern)
        {
            built.Append(character switch
            {
                '*' => ".*",
                '?' => ".",
                _ => Regex.Escape(character.ToString()),
            });
        }

        return built.Append('$').ToString();
    }
}
