using System;
using System.Collections.Generic;
using Arlecchino.Rendering.Text;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>One row of a wrapped line, and where in the text it begins.</summary>
/// <param name="Start">Index in the text of the first character on the row.</param>
/// <param name="Text">What the row says.</param>
internal readonly record struct CommandLineRow(int Start, string Text);

/// <summary>
/// Breaks what is being typed into rows, keeping where in the text each row starts. Wrapping text that is
/// only read can drop the spaces it broke at; a line being edited cannot, since the caret counts them.
/// </summary>
internal static class CommandLineWrap
{
    /// <summary>Breaks the text into rows, at the last space that fits and mid-word when there is none.</summary>
    /// <param name="text">What is on the line.</param>
    /// <param name="width">Columns a row has; anything below one is treated as one.</param>
    /// <returns>The rows, in order, together holding every character of the text.</returns>
    public static List<CommandLineRow> Rows(string text, int width)
    {
        var columns = Math.Max(1, width);
        var rows = new List<CommandLineRow>();
        var start = 0;

        while (true)
        {
            var fits = Fitting(text, start, columns);

            if (fits >= text.Length)
            {
                rows.Add(new(start, text[start..]));

                return rows;
            }

            var cut = Breaking(text, start, fits);

            rows.Add(new(start, text[start..cut]));
            start = cut;
        }
    }

    /// <summary>Where the caret sits once the text has been broken up.</summary>
    /// <param name="rows">The rows the text was broken into.</param>
    /// <param name="cursor">Where the caret is, counted from the start of the text in characters.</param>
    /// <returns>The row the caret is on and how many columns into it the caret stands.</returns>
    public static (int Row, int Column) Caret(List<CommandLineRow> rows, int cursor)
    {
        for (var row = rows.Count - 1; row >= 0; row--)
        {
            var (start, text) = rows[row];

            if (cursor >= start)
            {
                return (row, TextWidth.Of(text[..Math.Min(text.Length, cursor - start)]));
            }
        }

        return (0, 0);
    }

    /// <summary>
    /// How much of the text from a place fills a row. A symbol wider than the whole row is taken whole
    /// anyway, since a row that took nothing would never end.
    /// </summary>
    /// <param name="text">What is on the line.</param>
    /// <param name="start">Where the row begins.</param>
    /// <param name="columns">Columns the row has.</param>
    /// <returns>The index just past the last character that fits.</returns>
    private static int Fitting(string text, int start, int columns)
    {
        var fits = start + TextWidth.Truncate(text[start..], columns).Length;

        return fits > start ? fits : start + TextWidth.NextClusterLength(text, start);
    }

    /// <summary>
    /// Where to break a row that has more text after it. The space is left at the end of the row it broke
    /// at rather than thrown away, so every character keeps a place the caret can be counted to.
    /// </summary>
    /// <param name="text">What is on the line.</param>
    /// <param name="start">Where the row begins.</param>
    /// <param name="fits">The index just past the last character that fits.</param>
    /// <returns>Where the next row begins.</returns>
    private static int Breaking(string text, int start, int fits)
    {
        var space = text.LastIndexOf(' ', fits - 1, fits - start);

        return space > start ? space + 1 : fits;
    }
}
