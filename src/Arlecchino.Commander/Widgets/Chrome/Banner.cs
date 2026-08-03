using Arlecchino.Commander.Stores;
using Arlecchino.Rendering;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// The band along the top: what this is, which tabs are open, and the one key that leads everywhere.
/// It is drawn on the lit surface, so the step down to the panels marks the edge between them without
/// a rule having to be spent on it.
///
/// What the tabs do with the room left between the name and the hint is <see cref="TabStrip"/>'s.
/// </summary>
public sealed class Banner
{
    /// <summary>How many rows it takes.</summary>
    public const int Height = 1;

    private const int TabRow = 0;

    /// <summary>What is kept between the last thing the tabs draw and the line about the palette.</summary>
    private const int Apart = 2;

    private readonly TabStrip _strip;

    /// <summary>Draws the band over a set of tabs.</summary>
    /// <param name="sessions">The sessions there are, and which of them is open.</param>
    public Banner(Sessions sessions) => _strip = new(sessions);

    /// <summary>Draws it.</summary>
    /// <param name="header">The row to draw on.</param>
    public void Draw(SurfaceRegion header)
    {
        var coat = Skin.Lively;

        header.Fill(coat.Text);
        header = header.Inset(new Margin(2, 0, 2, 0));

        if (header.Height < Height)
        {
            return;
        }

        var name = Loc(LocString.HeaderName);
        var kind = Loc(LocString.HeaderKind);
        var palette = Loc(LocString.HeaderPalette);
        var column = 0;

        header.Write(TabRow, column, "◆", coat.Accent);

        column += 2;
        header.Write(TabRow, column, name, coat.Strong);

        column += name.Length + 1;
        header.Write(TabRow, column, kind, coat.Faded);

        column += kind.Length + 1;

        header.WriteLine(TabRow, palette, coat.Faded, Align.Right);
        _strip.Draw(header.Inset(new Margin(column, 0, palette.Length + Apart, 0)));
    }

    /// <summary>
    /// What a click on the band landed on. The click arrives in frame cells and the tabs were measured
    /// inside a strip that sits well in from the edge of a content area that is itself inset — so the
    /// two are put in the same coordinates rather than assumed to already share them.
    /// </summary>
    /// <param name="row">Which row of the frame it was on.</param>
    /// <param name="column">How far along that row.</param>
    /// <returns>What it landed on, or nothing when it landed on none of it.</returns>
    public TabHit? Tab(int row, int column) => _strip.At(row, column);

    /// <summary>Scrolls the tabs, for a click on one of the markers.</summary>
    /// <param name="by">Which way, and how far.</param>
    public void Scroll(int by) => _strip.Scroll(by);
}
