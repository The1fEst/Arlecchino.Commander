using Arlecchino.Rendering;
using Arlecchino.Rendering.Text;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// The frame every screen that is not the panels is drawn in: a band naming it, a hairline, the body, a
/// hairline, and a band along the bottom saying what is going on and which keys leave.
/// </summary>
public static class Sheet
{
    /// <summary>How many rows the band at the top takes, rule included.</summary>
    public const int Head = 3;

    /// <summary>How many rows the band along the bottom takes, rule included.</summary>
    public const int Foot = 2;

    /// <summary>Draws the band at the top.</summary>
    /// <param name="header">The rows to draw on.</param>
    /// <param name="title">What the screen is.</param>
    /// <param name="said">What it is doing, under the title.</param>
    public static void Title(SurfaceRegion header, string title, string said)
    {
        var coat = Skin.Terminal;

        header.Fill(coat.Text);

        if (header.Height < Head)
        {
            return;
        }

        header.Write(0, 0, TextWidth.Truncate(title, header.Width), coat.Accent);
        header.Write(1, 0, TextWidth.Truncate(said, header.Width), coat.Meta);
        header.Rows(2, 1).Fill(coat.Rule, '─');
    }

    /// <summary>Draws the band along the bottom.</summary>
    /// <param name="footer">The rows to draw on.</param>
    /// <param name="said">What is going on, at the left.</param>
    /// <param name="hints">Which keys leave, at the right.</param>
    public static void Hints(SurfaceRegion footer, string said, string hints)
    {
        var coat = Skin.Terminal;

        footer.Fill(coat.Text);

        if (footer.Height < Foot)
        {
            return;
        }

        footer.Rows(0, 1).Fill(coat.Rule, '─');
        footer.Write(1, 0, TextWidth.Truncate(said, footer.Width), coat.Second);
        footer.WriteLine(1, hints, coat.Label, Align.Right);
    }
}
