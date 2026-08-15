using System;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Colors;

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
            typing ? Skin.Paint(Skin.Crimson, Skin.Unlit, TextStyle.Bold) : coat.Sleeping);

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
        var (caret, column) = CommandLineWrap.Caret(rows, text.Cursor);
        var first = Math.Max(0, caret - MostRows + 1);
        var shown = Math.Min(MostRows, rows.Count - first);

        for (var row = 0; row < shown; row++)
        {
            region.Write(row, at, rows[first + row].Text, coat.Text);
        }

        if (typing)
        {
            region.Write(
                caret - first,
                at + column,
                text.Cursor < text.Text.Length ? text.Text[text.Cursor].ToString() : " ",
                Skin.Paint(Skin.Ink, Skin.Crimson));
        }

        return shown;
    }
}
