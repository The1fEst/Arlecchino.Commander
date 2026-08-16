using System;
using Arlecchino.Editing;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Colors;
using Arlecchino.Rendering.Text;
using Arlecchino.Widgets.Text;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// The rows the command line is drawn on: where the command would run, then the prompt, then what has been
/// typed, carried onto another row when it outgrows the first. A closed line drops the caret.
/// </summary>
internal static class CommandLinePaint
{
    private const int SideRoom = 2;
    private const int MostRows = 5;

    /// <summary>Draws the line.</summary>
    /// <param name="region">The rows to draw on.</param>
    /// <param name="text">What is written on the line and where the caret is.</param>
    /// <param name="typing">Whether the line has the keyboard.</param>
    /// <param name="prompt">Where the command would run.</param>
    /// <param name="tail">What the far end of the first row says.</param>
    /// <returns>How many rows the line asks for, which is known only once it has been drawn.</returns>
    public static int Draw(SurfaceRegion region, CommandLineText text, bool typing, string prompt, string tail)
    {
        var coat = Skin.Quiet;

        region.Fill(coat.Text);
        region.Write(0, SideRoom, prompt, typing ? coat.Faded : coat.Sleeping);

        var mark = SideRoom + prompt.Length + 1;

        region.Write(
            0,
            mark,
            "❯",
            typing ? Skin.Paint(Skin.Flame, Skin.Unlit, TextStyle.Bold) : coat.Sleeping);

        var at = mark + 2;
        var room = region.Width - at - tail.Length - SideRoom - 2;

        if (room <= 1)
        {
            return 1;
        }

        if (region.Width > at + room + tail.Length)
        {
            region.Write(0, region.Width - tail.Length - SideRoom, tail, coat.Ghost);
        }

        var rows = CommandLineWrap.Rows(text.Text, room);
        var (caret, _) = CommandLineWrap.Caret(rows, text.Caret);
        var first = Math.Max(0, caret - MostRows + 1);
        var shown = Math.Min(MostRows, rows.Count - first);

        var selection = TextEditing.Selection(text);

        for (var row = 0; row < shown; row++)
        {
            Row(region, rows[first + row], row, at, selection, typing && first + row == caret ? text.Caret : -1);
        }

        return shown;
    }

    /// <summary>
    /// Draws one row of what is typed, with whatever part of it the selection covers standing out and the
    /// caret on the symbol it stands on. A selection spanning rows is drawn as the piece of it each row holds.
    /// </summary>
    /// <param name="region">The rows to draw on.</param>
    /// <param name="row">The row and where in the text it starts.</param>
    /// <param name="at">Which row of the region it goes on.</param>
    /// <param name="column">The column the text starts at.</param>
    /// <param name="selection">Where the selection starts and ends in the whole text.</param>
    /// <param name="caret">Where the caret is in the whole text, or <c>-1</c> when it is not on this row.</param>
    private static void Row(
        SurfaceRegion region,
        CommandLineRow row,
        int at,
        int column,
        (int Start, int End) selection,
        int caret)
    {
        var coat = Skin.Quiet;
        var written = column;

        EntryRuns.Of(
            row.Text,
            caret < 0 ? -1 : Math.Clamp(caret - row.Start, 0, row.Text.Length),
            (Math.Clamp(selection.Start - row.Start, 0, row.Text.Length),
                Math.Clamp(selection.End - row.Start, 0, row.Text.Length)),
            Skin.Typed(coat.Text, Skin.Crimson),
            (piece, style) =>
            {
                region.Write(at, written, piece, style);
                written += TextWidth.Of(piece);
            });
    }
}
