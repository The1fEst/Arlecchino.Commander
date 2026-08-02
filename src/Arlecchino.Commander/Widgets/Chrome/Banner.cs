using System.Collections.Generic;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Colors;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// The band along the top: what this is, which tabs are open, and the one key that leads everywhere.
/// It is drawn on the lit surface, so the step down to the panels marks the edge between them without
/// a rule having to be spent on it.
/// </summary>
public sealed class Banner
{
    /// <summary>How many rows it takes.</summary>
    public const int Height = 1;

    /// <summary>What a click on the last tab means: not a tab, but the making of one.</summary>
    public const int Fresh = -1;

    private const int TabRow = 0;

    private readonly Panels _panels;
    private readonly List<(int Column, int Width, int Index)> _tabs = [];

    /// <summary>Draws the band over a set of tabs.</summary>
    /// <param name="panels">The tabs, and which of them is open.</param>
    public Banner(Panels panels) => _panels = panels;

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
        var column = 0;

        header.Write(TabRow, column, "◆", coat.Accent);

        column += 2;
        header.Write(TabRow, column, name, coat.Strong);

        column += name.Length + 1;
        header.Write(TabRow, column, kind, coat.Faded);

        _tabs.Clear();

        column += kind.Length + 1;
        for (var index = 0; index < _panels.Sessions.Count; index++)
        {
            var session = _panels.Sessions[index];
            var label = session.Label;
            var live = index == _panels.Open.Value;

            if (column + label.Length + 6 > header.Width - 4)
            {
                break;
            }

            var under = live ? Skin.Chip : Skin.Lit;
            var lit = new Skin.Coat(under);

            header.Write(TabRow, column, new(' ', label.Length + 5), Skin.Paint(Skin.Bone, under));
            Sides(header, column + 1, session, live, lit);

            _tabs.Add((column, label.Length + 5, index));

            column += label.Length + 6;
        }

        header.WriteLine(TabRow, Loc(LocString.HeaderPalette), coat.Faded, Align.Right);
        header.Write(TabRow, column + 1, "+", coat.Trace);

        _tabs.Add((column, 3, Fresh));
    }

    /// <summary>Which tab a click landed on.</summary>
    /// <param name="column">How far along the row it was.</param>
    /// <returns>The session it belongs to, or nothing when it hit the gap between two.</returns>
    public int? Tab(int column)
    {
        foreach (var (at, width, index) in _tabs)
        {
            if (column >= at && column < at + width)
            {
                return index;
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
        var right = live && _panels.RightIsActive.Value;
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
