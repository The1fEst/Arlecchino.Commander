using System;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Text;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// The shape everything that says what could be pressed or typed next is drawn in: a rule down the left
/// edge, a heading in small capitals, and the rows under it. No border.
/// </summary>
internal static class HintBox
{
    private const int SideRoom = 2;
    private const int Padding = 3;
    private const int Content = 2;

    /// <summary>
    /// Puts the box at the foot of the region, on the right, and gives back the rows inside it. What goes
    /// in them is the caller's.
    /// </summary>
    /// <param name="region">Everything above the line, which the box places itself at the foot of.</param>
    /// <param name="title">What the box is called, drawn in small capitals.</param>
    /// <param name="inner">How wide the rows want to be.</param>
    /// <param name="rows">How many rows there are.</param>
    /// <returns>The rows to write into, which is empty when there was no room for the box.</returns>
    public static SurfaceRegion Open(SurfaceRegion region, string title, int inner, int rows)
    {
        var room = Math.Min(inner, region.Width - (SideRoom * 2) - Padding);
        var height = rows + 1;

        if (rows <= 0 || room <= 0 || region.Height < height + 2)
        {
            return default;
        }

        var coat = Skin.Overlay;
        var width = room + Padding;
        var box = region
            .Rows(region.Height - height - 1, height)
            .Inset(new Margin(Math.Max(0, region.Width - width - SideRoom), 0, SideRoom, 0));

        box.Fill(coat.Text);
        box.Inset(new Margin(0, 0, box.Width - 1, 0)).Fill(Skin.CrimsonFill);

        var content = box.Inset(new Margin(Content, 0, 1, 0));

        content.Write(0, 0, TextWidth.Truncate(title.ToUpperInvariant(), content.Width), coat.Label);

        return content.Rows(1, rows);
    }
}
