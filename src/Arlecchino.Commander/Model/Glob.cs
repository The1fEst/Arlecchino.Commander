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
