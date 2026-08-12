using System;
using System.Collections.Generic;
using Arlecchino.Commander.Stores;
using Arlecchino.Rendering;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>What part of the strip a click landed on.</summary>
public enum TabPart
{
    /// <summary>A tab, which is to be shown.</summary>
    Tab,

    /// <summary>The cross on a tab, which is to close it.</summary>
    Close,

    /// <summary>The plus at the end, which is not a tab but the making of one.</summary>
    Fresh,

    /// <summary>The marker at one end, which scrolls the strip that way.</summary>
    Scroll,
}

/// <summary>What a click on the strip landed on.</summary>
/// <param name="Part">Which part.</param>
/// <param name="Index">Which tab, or which way to scroll: <c>-1</c> back and <c>1</c> on.</param>
public readonly record struct TabHit(TabPart Part, int Index);

/// <summary>
/// The tabs along the top, in whatever room the band can spare them. More tabs than room shortens the
/// names first, down to where one still says something, and past that the strip scrolls.
/// </summary>
public sealed class TabStrip
{
    private const int Row = 0;

    /// <summary>What the plus at the end costs, with the space before it.</summary>
    private const int Plus = 2;

    /// <summary>What a marker at either end costs, with the space beside it.</summary>
    private const int Marker = 2;

    private readonly Sessions _sessions;
    private readonly TabWindow _window = new();
    private readonly List<(int Column, int Width, TabPart Part, int Index)> _spots = [];

    private SurfaceRegion _strip;

    /// <summary>Draws the tabs of a set of sessions.</summary>
    /// <param name="sessions">The sessions there are, and which of them is open.</param>
    public TabStrip(Sessions sessions) => _sessions = sessions;

    /// <summary>Draws them.</summary>
    /// <param name="strip">The room the band can spare, which is everything between the name and the hint.</param>
    public void Draw(SurfaceRegion strip)
    {
        _strip = strip;
        _spots.Clear();

        if (strip.Width <= Plus || strip.Height < 1)
        {
            return;
        }

        var closable = _sessions.All.Count > 1;
        var room = strip.Width - Plus;
        var most = Sharing(room, closable);
        var widths = Widths(most, closable);
        var scrolls = Together(widths) > room;
        var last = _window.Showing(widths, scrolls ? room - (Marker * 2) : room, _sessions.Open.Value);
        var column = scrolls ? Marker : 0;

        if (scrolls)
        {
            Arrow(strip, 0, "‹", _window.First > 0, -1);
        }

        for (var index = _window.First; index < last; index++)
        {
            Tab(strip, column, index, most, closable);

            column += widths[index] + TabWindow.Between;
        }

        if (scrolls)
        {
            Arrow(strip, column, "›", last < widths.Count, 1);

            column += Marker;
        }

        strip.Write(Row, column + 1, "+", Skin.Lively.Trace);
        _spots.Add((column, Plus + 1, TabPart.Fresh, 0));
    }

    /// <summary>What a click landed on.</summary>
    /// <param name="row">Which row of the frame it was on.</param>
    /// <param name="column">How far along that row.</param>
    /// <returns>What it landed on, or nothing when it landed on none of it.</returns>
    public TabHit? At(int row, int column)
    {
        if (!_strip.Contains(row, column))
        {
            return null;
        }

        var (_, along) = _strip.ToLocal(row, column);

        foreach (var (at, width, part, index) in _spots)
        {
            if (along >= at && along < at + width)
            {
                return new(part, index);
            }
        }

        return null;
    }

    /// <summary>Scrolls the strip, for a click on one of the markers.</summary>
    /// <param name="by">Which way, and how far.</param>
    public void Scroll(int by) => _window.Scroll(by, _sessions.All.Count);

    /// <summary>
    /// How wide a name may be, given how many tabs there are and how much strip there is. Tabs share what
    /// there is equally, so a tab opened does not push one already open off the end.
    /// </summary>
    /// <param name="room">The cells the tabs have between them.</param>
    /// <param name="closable">Whether tabs wear a cross.</param>
    /// <returns>The widest a name may be, or <see cref="TabFace.Whole"/> when none need shortening.</returns>
    private int Sharing(int room, bool closable)
    {
        var cross = closable ? TabFace.Crossed : 0;
        var wanted = 0;

        foreach (var session in _sessions.All)
        {
            wanted += session.Label.Length + TabFace.Chrome + cross + TabWindow.Between;
        }

        if (wanted <= room)
        {
            return TabFace.Whole;
        }

        var share = (room / _sessions.All.Count) - TabWindow.Between - TabFace.Chrome - cross;

        return Math.Max(TabFace.Least, share);
    }

    /// <summary>How wide each tab comes out once its name has been shortened to what it may take.</summary>
    /// <param name="most">The widest a name may be.</param>
    /// <param name="closable">Whether tabs wear a cross.</param>
    /// <returns>One width per tab.</returns>
    private List<int> Widths(int most, bool closable)
    {
        var widths = new List<int>(_sessions.All.Count);

        foreach (var session in _sessions.All)
        {
            widths.Add(TabFace.Width(session, most, closable));
        }

        return widths;
    }

    private static int Together(List<int> widths)
    {
        var total = 0;

        foreach (var width in widths)
        {
            total += width + TabWindow.Between;
        }

        return total;
    }

    /// <summary>Draws one marker, lit when there is something that way and dim when there is not.</summary>
    /// <param name="strip">Where to draw.</param>
    /// <param name="column">Where the marker goes.</param>
    /// <param name="glyph">Which way it points.</param>
    /// <param name="more">Whether there are tabs that way.</param>
    /// <param name="by">What a click on it scrolls by.</param>
    private void Arrow(SurfaceRegion strip, int column, string glyph, bool more, int by)
    {
        var coat = Skin.Lively;

        strip.Write(Row, column, glyph, more ? coat.Second : coat.Trace);

        if (more)
        {
            _spots.Add((column, Marker, TabPart.Scroll, by));
        }
    }

    /// <summary>Draws one tab, and remembers where it and its cross went.</summary>
    /// <param name="strip">Where to draw.</param>
    /// <param name="column">Where the tab goes.</param>
    /// <param name="index">Which tab it is.</param>
    /// <param name="most">The widest a name may be.</param>
    /// <param name="closable">Whether tabs wear a cross.</param>
    private void Tab(SurfaceRegion strip, int column, int index, int most, bool closable)
    {
        var session = _sessions.All[index];
        var live = index == _sessions.Open.Value;
        var right = live ? _sessions.RightIsActive.Value : session.RightIsActive;
        var label = TabFace.Draw(strip, column, session, most, new(live, right, closable));

        _spots.Add((column, label + TabFace.Chrome - (closable ? TabFace.Crossed : 0), TabPart.Tab, index));

        if (closable)
        {
            _spots.Add((column + label + 3, TabFace.Crossed + 1, TabPart.Close, index));
        }
    }
}
