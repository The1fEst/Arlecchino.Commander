using System;
using Arlecchino.Rendering;

namespace Arlecchino.Commander.Widgets;

/// <summary>
/// Draws a <see cref="Choosing"/>. It is the same overlay as the operation dialog wears — the same
/// surface, the same rules, the same place on screen — so a list and a question read as two things the
/// application says rather than two applications.
/// </summary>
public static class ChoiceBox
{
    private const int Wanted = 56;
    private const int Padding = 2;
    private const int MostRows = 12;

    /// <summary>
    /// What the box costs besides its rows: the rule, the title, the query, and a blank row above the
    /// footer and below it. The blank above the footer is what keeps the keys from reading as one more
    /// thing in the list.
    /// </summary>
    private const int Chrome = 8;

    /// <summary>Draws the list over whatever is behind it.</summary>
    /// <param name="screen">The whole screen.</param>
    /// <param name="choosing">What is being picked from.</param>
    public static void Draw(SurfaceRegion screen, Choosing choosing)
    {
        ArgumentNullException.ThrowIfNull(choosing);

        var shown = Math.Min(MostRows, Math.Max(1, choosing.Matching.Count));
        var rows = shown + Chrome;
        var width = Math.Min(Wanted, screen.Width - 4);

        if (width < 30 || screen.Height < rows + 2)
        {
            return;
        }

        var left = (screen.Width - width) / 2;
        var box = screen
            .Rows(Math.Max(0, (screen.Height - rows) / 3), rows)
            .Inset(new Margin(left, 0, screen.Width - width - left, 0));
        var coat = Skin.Overlay;

        box.Fill(coat.Text);
        box.Rows(0, 1).Fill(Skin.Paint(Skin.Crimson, Skin.Crimson));

        var inside = box.Rows(1, rows - 1).Inset(new Margin(Padding, 0, Padding, 0));

        inside.Write(1, 0, choosing.Title, coat.Strong);
        inside.WriteLine(1, Counted(choosing), coat.Label, Align.Right);

        Query(inside, choosing, coat);
        Rows(inside, choosing, coat, shown);

        inside.Write(inside.Height - 2, 0, choosing.Footer, coat.Label);
    }

    private static string Counted(Choosing choosing) => choosing.Typed.Length == 0
        ? choosing.Items.Count == 1 ? "1" : $"{choosing.Items.Count}"
        : $"{choosing.Matching.Count} of {choosing.Items.Count}";

    /// <summary>
    /// The line what is typed lands on. It is drawn whether or not anything has been typed, so nobody
    /// has to discover that the list narrows — the prompt is there saying it does.
    /// </summary>
    /// <param name="inside">Where to draw.</param>
    /// <param name="choosing">What is being picked from.</param>
    /// <param name="coat">The surface underneath.</param>
    private static void Query(SurfaceRegion inside, Choosing choosing, Skin.Coat coat)
    {
        inside.Write(2, 0, "❯", coat.Accent);

        if (choosing.Typed.Length > 0)
        {
            inside.Write(2, 2, TextWidth.Truncate(choosing.Typed, inside.Width - 4), coat.Text);
            inside.Write(2, 2 + TextWidth.Of(choosing.Typed), " ", Skin.Paint(Skin.Ink, Skin.Crimson));

            return;
        }

        inside.Write(2, 2, " ", Skin.Paint(Skin.Ink, Skin.Crimson));
        inside.Write(2, 4, "type to narrow the list", coat.Ghost);
    }

    private static void Rows(SurfaceRegion inside, Choosing choosing, Skin.Coat coat, int shown)
    {
        if (choosing.Matching.Count == 0)
        {
            inside.Write(4, 0, "nothing here goes by that name", coat.Label);

            return;
        }

        var first = Math.Max(0, Math.Min(choosing.Chosen - (shown / 2), choosing.Matching.Count - shown));

        for (var index = 0; index < shown && first + index < choosing.Matching.Count; index++)
        {
            var pick = choosing.Matching[first + index];
            var here = first + index == choosing.Chosen;
            var row = inside.Rows(4 + index, 1);

            if (here)
            {
                row.Fill(Skin.ChosenRow);
            }

            row.Write(0, 0, TextWidth.Truncate(pick.Label, row.Width - pick.Hint.Length - 2),
                here ? Skin.ChosenName : coat.Text);

            if (pick.Hint.Length > 0)
            {
                row.WriteLine(0, pick.Hint, here ? Skin.ChosenMeta : coat.Label, Align.Right);
            }
        }
    }
}
