using System.Collections.Generic;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Colors;

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
    private const int Reserved = 4;

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
        var palette = Loc(LocString.HeaderPalette);
        var room = header.Width - palette.Length - Reserved;

        column += kind.Length + 1;
        for (var index = 0; index < _sessions.All.Count; index++)
        {
            var session = _sessions.All[index];
            var label = session.Label;
            var live = index == _sessions.Open.Value;
            var width = label.Length + Chrome + (closable ? Crossed : 0);

            if (column + width + 1 > room)
            {
                break;
            }

            var under = live ? Skin.Chip : Skin.Lit;
            var lit = new Skin.Coat(under);

            header.Write(TabRow, column, new(' ', width), Skin.Paint(Skin.Bone, under));
            Sides(header, column + 1, session, live, lit);

            _tabs.Add((column, closable ? label.Length + Chrome - Crossed : width, TabPart.Tab, index));

            if (closable)
            {
                header.Write(TabRow, column + label.Length + 4, "×", live ? lit.Text : lit.Trace);

                _tabs.Add((column + label.Length + 3, Crossed + 1, TabPart.Close, index));
            }

            column += width + 1;
        }

        header.WriteLine(TabRow, palette, coat.Faded, Align.Right);
        header.Write(TabRow, column + 1, "+", coat.Trace);

        _tabs.Add((column, 3, TabPart.Fresh, 0));
    }

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
    /// The two sides of a tab, with the lit dot against whichever of them is being worked in. A tab
    /// holds two panels, so the dot is the only thing on it that can answer which of the two has the
    /// focus — a dot that never moves answers nothing. A side on a server is named after it, in the
    /// colour servers get, so a glance at the tab says what it is connected to.
    /// </summary>
    /// <param name="header">The band to draw on.</param>
    /// <param name="column">Where the tab's text starts.</param>
    /// <param name="session">The tab.</param>
    /// <param name="live">Whether it is the tab on screen.</param>
    /// <param name="lit">The surface of the tab.</param>
    private void Sides(SurfaceRegion header, int column, Session session, bool live, Skin.Coat lit)
    {
        var right = live && _sessions.RightIsActive.Value;
        var near = Named(session.Left, live && !right, lit);
        var far = Named(session.Right, right, lit);
        var at = column;

        if (!right)
        {
            header.Write(TabRow, at, "●", live ? lit.Accent : lit.Trace);
            at += 2;
        }

        header.Write(TabRow, at, session.Near, near);
        at += session.Near.Length + 1;

        header.Write(TabRow, at, "⇄", lit.Trace);
        at += 2;

        if (right)
        {
            header.Write(TabRow, at, "●", lit.Accent);
            at += 2;
        }

        header.Write(TabRow, at, session.Far, far);
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
