using System;
using System.Collections.Generic;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Text;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// What finishes the chord that has been started, listed while the framework waits for it. The keys wear
/// the chip the bar of function keys wears.
/// </summary>
internal static class KeyHints
{
    private const int MostRows = 12;
    private const int Gap = 2;
    private const int Chip = 2;

    /// <summary>Draws them, or nothing at all when no chord is waiting.</summary>
    /// <param name="region">Everything above the line, which the box places itself at the foot of.</param>
    /// <param name="title">What the box is called.</param>
    /// <param name="keys">Each key that would finish the chord, and what it would do.</param>
    public static void Draw(SurfaceRegion region, string title, IReadOnlyList<(string Key, string Description)> keys)
    {
        if (keys.Count == 0)
        {
            return;
        }

        var showing = Math.Min(keys.Count, MostRows);
        var chips = 0;
        var labels = 0;

        for (var at = 0; at < showing; at++)
        {
            chips = Math.Max(chips, TextWidth.Of(keys[at].Key) + Chip);
            labels = Math.Max(labels, TextWidth.Of(keys[at].Description));
        }

        var rows = HintBox.Open(region, title, chips + Gap + labels, showing);

        if (rows.IsEmpty)
        {
            return;
        }

        var coat = Skin.Overlay;
        var room = rows.Width - chips - Gap;

        for (var at = 0; at < showing; at++)
        {
            var (key, description) = keys[at];

            rows.Write(at, 0, TextWidth.PadRight($" {key} ", chips), Skin.Paint(Skin.Sea, Skin.Chip));

            if (room > 0)
            {
                rows.Write(at, chips + Gap, TextWidth.Truncate(description, room), coat.Second);
            }
        }
    }
}
