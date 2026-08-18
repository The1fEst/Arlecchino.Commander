using System;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Text;
using Arlecchino.Widgets.Text;
using Arlecchino.Commander.Widgets.Chrome;

namespace Arlecchino.Commander.Widgets.Dialogs;

/// <summary>
/// Where a list ended up on screen, so a click can be told what it landed on. The box places itself while
/// drawing, so drawing is what answers this rather than a second pass over the same arithmetic.
/// </summary>
/// <param name="Box">The whole box, for telling a click on it from a click outside.</param>
/// <param name="Rows">The rows that are showing.</param>
/// <param name="First">Which entry the topmost showing row is.</param>
public readonly record struct ChoiceSpots(SurfaceRegion Box, SurfaceRegion Rows, int First);

/// <summary>
/// Draws a <see cref="Choosing"/>. It is the same overlay as the operation dialog wears — the same
/// surface, the same rules, the same place on screen — so a list and a question read as two things the
/// application says rather than two applications.
/// </summary>
public static class ChoiceBox
{
    private const int Width = 56;
    private const int Padding = 2;
    private const int MostRows = 12;

    /// <summary>
    /// What the box costs besides its rows: the rule, the title, the query, and a blank row above the
    /// footer and below it. The blank above the footer keeps the keys out of the list.
    /// </summary>
    private const int Chrome = 8;

    /// <summary>
    /// How much of a row the name may take before what qualifies it starts, fixed rather than measured so
    /// that the hints line up. A list whose rows are names alone gives the whole row to the name.
    /// </summary>
    private const int Naming = 24;

    /// <summary>Draws the list over whatever is behind it.</summary>
    /// <param name="screen">The whole screen.</param>
    /// <param name="choosing">What is being picked from.</param>
    /// <returns>Where it landed, for the clicks.</returns>
    public static ChoiceSpots Draw(SurfaceRegion screen, Choosing choosing)
    {
        var showing = Math.Min(MostRows, Math.Max(1, choosing.Matching.Count));
        var rows = showing + Chrome;
        var width = Math.Min(Width, screen.Width - 4);

        if (width < 30 || screen.Height < rows + 2)
        {
            return default;
        }

        var left = (screen.Width - width) / 2;
        var box = screen
            .Rows(Math.Max(0, (screen.Height - rows) / 3), rows)
            .Inset(new Margin(left, 0, screen.Width - width - left, 0));
        var coat = Skin.Overlay;

        box.Fill(coat.Text);
        box.Rows(0, 1).Fill(Skin.Paint(Skin.Crimson, Skin.Crimson));

        var content = box.Rows(1, rows - 1).Inset(new Margin(Padding, 0, Padding, 0));

        content.Write(1, 0, choosing.Title, coat.Strong);
        content.WriteLine(1, Counted(choosing), coat.Label, Align.Right);

        Query(content, choosing, coat);

        var first = Rows(content, choosing, coat, showing);

        content.Write(content.Height - 2, 0, choosing.Footer, coat.Label);

        return new(box, content.Rows(4, showing), first);
    }

    private static string Counted(Choosing choosing) => choosing.Text.Length == 0
        ? choosing.Items.Count == 1 ? "1" : $"{choosing.Items.Count}"
        : Loc(LocString.ChoosingCount, choosing.Matching.Count, choosing.Items.Count);

    /// <summary>
    /// The line what is typed lands on. It is drawn whether anything has been typed or not, so the prompt
    /// itself says that the list narrows.
    /// </summary>
    /// <param name="content">Where to draw.</param>
    /// <param name="choosing">What is being picked from.</param>
    /// <param name="coat">The surface underneath.</param>
    private static void Query(SurfaceRegion content, Choosing choosing, Skin.Coat coat)
    {
        content.Write(2, 0, "❯", coat.Accent);

        var run = EntryRow.Draw(
            content,
            2,
            2,
            Math.Max(0, content.Width - 4),
            choosing.Filter,
            Skin.Entry(coat.Text, Skin.Crimson));

        if (choosing.Text.Length == 0)
        {
            content.Write(2, 2 + run + 1, Loc(LocString.ChoosingNarrow), coat.Ghost);
        }
    }

    /// <summary>Draws the rows that fit, scrolled to keep the chosen one in view.</summary>
    /// <param name="content">Where to draw.</param>
    /// <param name="choosing">What is being picked from.</param>
    /// <param name="coat">The surface underneath.</param>
    /// <param name="showing">How many rows there is room for.</param>
    /// <returns>Which entry the topmost row is, which is what turns a click into an entry.</returns>
    private static int Rows(SurfaceRegion content, Choosing choosing, Skin.Coat coat, int showing)
    {
        if (choosing.Matching.Count == 0)
        {
            content.Write(4, 0, Loc(LocString.ChoosingNothing), coat.Label);

            return 0;
        }

        var first = Math.Max(0, Math.Min(choosing.ChosenIndex - (showing / 2), choosing.Matching.Count - showing));

        for (var index = 0; index < showing && first + index < choosing.Matching.Count; index++)
        {
            var pick = choosing.Matching[first + index];
            var here = first + index == choosing.ChosenIndex;
            var row = content.Rows(4 + index, 1);
            var qualified = pick.Hint.Length + pick.Key.Length > 0;
            var name = qualified
                ? Math.Min(Naming, row.Width - pick.Hint.Length - pick.Key.Length - 4)
                : row.Width;

            if (here)
            {
                row.Fill(Skin.ChosenRow);
            }

            row.Write(0,
                0,
                TextWidth.Truncate(pick.Label, Math.Max(1, name)),
                here ? Skin.ChosenName : coat.Text);

            if (pick.Hint.Length > 0 && name > 0)
            {
                row.Write(0,
                    name + 2,
                    TextWidth.Truncate(pick.Hint, row.Width - name - pick.Key.Length - 4),
                    here ? Skin.ChosenMeta : coat.Label);
            }

            if (pick.Key.Length > 0)
            {
                row.WriteLine(0, pick.Key, here ? Skin.ChosenMeta : coat.Hint, Align.Right);
            }
            else if (pick.Hint.Length > 0 && name <= 0)
            {
                row.WriteLine(0, pick.Hint, here ? Skin.ChosenMeta : coat.Label, Align.Right);
            }
        }

        return first;
    }
}
