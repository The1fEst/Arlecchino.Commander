using System;
using System.Collections.Generic;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Colors;
using Arlecchino.Rendering.Text;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>What part of the band a click landed on.</summary>
public enum TabPart
{
    /// <summary>A tab, which is to be shown.</summary>
    Tab,

    /// <summary>The cross on a tab, which is to close it.</summary>
    Close,

    /// <summary>The plus at the end, which is not a tab but the making of one.</summary>
    Fresh,
}

/// <summary>What a click on the band landed on.</summary>
/// <param name="Part">Which part.</param>
/// <param name="Index">Which tab, where the part belongs to one.</param>
public readonly record struct TabHit(TabPart Part, int Index);

/// <summary>
/// The band along the top: what this is, which tabs are open, and the one key that leads everywhere.
/// It is drawn on the lit surface, so the step down to the panels marks the edge between them without
/// a rule having to be spent on it.
/// </summary>
public sealed class Banner
{
    /// <summary>How many rows it takes.</summary>
    public const int Height = 1;

    private const int TabRow = 0;

    /// <summary>What a tab costs besides its name: the lit dot, the two edges and the space they sit in.</summary>
    private const int Chrome = 5;

    /// <summary>What the cross costs on top of that: a space to stand off the name, and itself.</summary>
    private const int Crossed = 2;

    /// <summary>
    /// What is kept clear at the right-hand end: the plus that opens a tab, and a gap so it does not
    /// read as part of the line about the palette that is written there.
    /// </summary>
    private const int Reserved = 3;

    /// <summary>What a name may take when there is room for every tab to be written out in full.</summary>
    private const int Whole = -1;

    /// <summary>
    /// The narrowest a name is cut to: a letter and an ellipsis on each side of the arrow. Below this
    /// both sides come out as bare ellipses, and a row of tabs that all say nothing is worse than a
    /// few that still hint at what they are on.
    /// </summary>
    private const int Least = 7;

    private readonly Sessions _sessions;
    private readonly List<(int Column, int Width, TabPart Part, int Index)> _tabs = [];

    private SurfaceRegion _band;

    /// <summary>Draws the band over a set of tabs.</summary>
    /// <param name="sessions">The sessions there are, and which of them is open.</param>
    public Banner(Sessions sessions) => _sessions = sessions;

    /// <summary>Draws it.</summary>
    /// <param name="header">The row to draw on.</param>
    public void Draw(SurfaceRegion header)
    {
        var coat = Skin.Lively;

        header.Fill(coat.Text);
        header = header.Inset(new Margin(2, 0, 2, 0));
        _band = header;

        if (header.Height < Height)
        {
            return;
        }

        var name = Loc(LocString.HeaderName);
        var kind = Loc(LocString.HeaderKind);
        var column = 0;

        header.Write(TabRow, column, "◆", coat.Accent);

        column += 2;
        header.Write(TabRow, column, name, coat.Strong);

        column += name.Length + 1;
        header.Write(TabRow, column, kind, coat.Faded);

        _tabs.Clear();

        var closable = _sessions.All.Count > 1;
        var cross = closable ? Crossed : 0;
        var palette = Loc(LocString.HeaderPalette);

        column += kind.Length + 1;

        var most = Sharing(header.Width - palette.Length - Reserved - column, cross);

        for (var index = 0; index < _sessions.All.Count; index++)
        {
            var session = _sessions.All[index];
            var (near, far) = Shortened(session, most);
            var label = near.Length + far.Length + 3;
            var live = index == _sessions.Open.Value;
            var width = label + Chrome + cross;

            if (column + width + 1 > header.Width - palette.Length - Reserved)
            {
                break;
            }

            var under = live ? Skin.Chip : Skin.Lit;
            var lit = new Skin.Coat(under);

            header.Write(TabRow, column, new(' ', width), Skin.Paint(Skin.Bone, under));
            Sides(header, column + 1, session, near, far, live, lit);

            _tabs.Add((column, closable ? label + Chrome - Crossed : width, TabPart.Tab, index));

            if (closable)
            {
                header.Write(TabRow, column + label + 4, "×", live ? lit.Text : lit.Trace);

                _tabs.Add((column + label + 3, Crossed + 1, TabPart.Close, index));
            }

            column += width + 1;
        }

        header.WriteLine(TabRow, palette, coat.Faded, Align.Right);
        header.Write(TabRow, column + 1, "+", coat.Trace);

        _tabs.Add((column, 3, TabPart.Fresh, 0));
    }

    /// <summary>
    /// How wide a name may be, given how many tabs there are and how much band there is.
    ///
    /// Tabs used to be drawn at whatever width their names came to until the next one would not fit,
    /// and then stop — so opening a fourth tab could take the third one off the screen, which is the
    /// wrong answer to running out of room: a tab that cannot be seen cannot be clicked, and the one
    /// dropped is not the one anybody would have chosen to drop. They share what there is instead, and
    /// a name too long for its share is cut with an ellipsis to say so.
    /// </summary>
    /// <param name="room">The cells the tabs have between them.</param>
    /// <param name="cross">What the closing cross costs, when there is one.</param>
    /// <returns>The widest a name may be, or <see cref="Whole"/> when none of them need cutting.</returns>
    private int Sharing(int room, int cross)
    {
        var wanted = 0;

        foreach (var session in _sessions.All)
        {
            wanted += session.Label.Length + Chrome + cross + 1;
        }

        if (wanted <= room)
        {
            return Whole;
        }

        return Math.Max(Least, (room / _sessions.All.Count) - 1 - Chrome - cross);
    }

    /// <summary>
    /// The two sides of a tab as they are to be written, each cut to half of what the name may take.
    /// Both sides are cut rather than one, since a tab says what it is by naming both of them and a
    /// full name beside a stub reads as though only one side went anywhere.
    /// </summary>
    /// <param name="session">The tab.</param>
    /// <param name="most">The widest the whole name may be.</param>
    /// <returns>What to write on each side.</returns>
    private static (string Near, string Far) Shortened(Session session, int most)
    {
        if (most == Whole || session.Label.Length <= most)
        {
            return (session.Near, session.Far);
        }

        var each = Math.Max(1, (most - 3) / 2);

        return (Cut(session.Near, each), Cut(session.Far, each));
    }

    /// <summary>One side, cut to fit, with an ellipsis where it was cut.</summary>
    /// <param name="text">The name.</param>
    /// <param name="room">The cells it has.</param>
    /// <returns>What to write.</returns>
    private static string Cut(string text, int room) =>
        text.Length <= room ? text : TextWidth.Truncate(text, room - 1) + "…";

    /// <summary>
    /// Which tab a click landed on. The click arrives in frame cells and the tabs were measured inside
    /// the band, which sits two cells in from a content area that is itself inset — so the two are put
    /// in the same coordinates here rather than being assumed to already share them.
    /// </summary>
    /// <param name="row">Which row of the frame it was on.</param>
    /// <param name="column">How far along that row.</param>
    /// <returns>What it landed on, or nothing when it landed on none of it.</returns>
    public TabHit? Tab(int row, int column)
    {
        if (!_band.Contains(row, column))
        {
            return null;
        }

        var (_, along) = _band.ToLocal(row, column);

        foreach (var (at, width, part, index) in _tabs)
        {
            if (along >= at && along < at + width)
            {
                return new(part, index);
            }
        }

        return null;
    }

    /// <summary>
    /// The two sides of a tab, with the dot against whichever of them is being worked in. A tab holds
    /// two panels, so the dot is the only thing on it that can answer which of the two has the focus —
    /// a dot that never moves answers nothing. A side on a server is named after it, in the colour
    /// servers get, so a glance at the tab says what it is connected to.
    ///
    /// Which side that is comes from the tab itself unless the tab is the one on screen. The store
    /// holds the side for the tab being worked in and hands it back to the tab when it is left, so a
    /// tab that is not on screen is the only one that knows where its own focus was — reading the
    /// store for it put the dot on the left of every tab in the band whatever side it was left on.
    /// </summary>
    /// <param name="header">The band to draw on.</param>
    /// <param name="column">Where the tab's text starts.</param>
    /// <param name="session">The tab.</param>
    /// <param name="near">What the left side is called, as it is to be written.</param>
    /// <param name="far">The same for the right.</param>
    /// <param name="live">Whether it is the tab on screen.</param>
    /// <param name="lit">The surface of the tab.</param>
    private void Sides(
        SurfaceRegion header,
        int column,
        Session session,
        string near,
        string far,
        bool live,
        Skin.Coat lit)
    {
        var right = live ? _sessions.RightIsActive.Value : session.RightIsActive;
        var dot = live ? lit.Accent : lit.Trace;
        var at = column;

        if (!right)
        {
            header.Write(TabRow, at, "●", dot);
            at += 2;
        }

        header.Write(TabRow, at, near, Named(session.Left, live && !right, lit));
        at += near.Length + 1;

        header.Write(TabRow, at, "⇄", lit.Trace);
        at += 2;

        if (right)
        {
            header.Write(TabRow, at, "●", dot);
            at += 2;
        }

        header.Write(TabRow, at, far, Named(session.Right, live && right, lit));
    }

    /// <summary>What colour one side of a tab is written in.</summary>
    /// <param name="state">The panel that side holds.</param>
    /// <param name="working">Whether it is the side being worked in.</param>
    /// <param name="lit">The surface of the tab.</param>
    /// <returns>The style.</returns>
    private static TermColor Named(PanelState state, bool working, Skin.Coat lit) => state.Source.IsRemote
        ? lit.Remote
        : working
            ? lit.Text
            : lit.Meta;
}
