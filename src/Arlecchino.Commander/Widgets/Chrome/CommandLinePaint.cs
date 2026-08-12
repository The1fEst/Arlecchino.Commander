using System;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Colors;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// The one row the command line is drawn on: where the command would run, then the prompt, then what has
/// been typed. A closed line dims the path and drops the chevron and the caret.
/// </summary>
internal static class CommandLinePaint
{
    private const int SideRoom = 2;

    /// <summary>Draws the row.</summary>
    /// <param name="region">The row to draw on.</param>
    /// <param name="text">What is written on the line and where the caret is.</param>
    /// <param name="typing">Whether the line has the keyboard.</param>
    /// <param name="prompt">Where the command would run.</param>
    /// <param name="tail">What the far end of the row says.</param>
    public static void Draw(SurfaceRegion region, CommandLineText text, bool typing, string prompt, string tail)
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
            return;
        }

        var offset = Math.Max(0, text.Cursor - room + 1);

        region.Write(0, at, text.Text[offset..Math.Min(text.Text.Length, offset + room)], coat.Text);

        if (typing)
        {
            region.Write(
                0,
                at + (text.Cursor - offset),
                text.Cursor < text.Text.Length ? text.Text[text.Cursor].ToString() : " ",
                Skin.Paint(Skin.Ink, Skin.Crimson));
        }

        if (region.Width > at + room + tail.Length)
        {
            region.Write(0, region.Width - tail.Length - SideRoom, tail, coat.Ghost);
        }
    }
}
