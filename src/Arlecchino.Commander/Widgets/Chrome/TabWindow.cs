using System;
using System.Collections.Generic;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// Which of the tabs are showing, when there is not room for all of them.
///
/// It holds the one piece of state the band has: how far it has been scrolled. That is why it is a
/// thing of its own rather than a calculation done while drawing — a scroll position worked out afresh
/// every frame is not a scroll position, it is wherever the arithmetic happens to land.
/// </summary>
public sealed class TabWindow
{
    /// <summary>The gap kept between one tab and the next.</summary>
    public const int Between = 1;

    private int _first;
    private int _seen = -1;

    /// <summary>The first tab showing.</summary>
    public int First => _first;

    /// <summary>Scrolls it, for a click on one of the markers.</summary>
    /// <param name="by">Which way, and how far.</param>
    /// <param name="count">How many tabs there are.</param>
    public void Scroll(int by, int count) => _first = Math.Clamp(_first + by, 0, Math.Max(0, count - 1));

    /// <summary>
    /// Works out what is showing. Going to a tab brings it into view, however far the strip had been
    /// scrolled — a strip that has scrolled away from the panels being worked in says nothing about
    /// where the work is.
    ///
    /// Only going to one, though. Scrolling with the markers is allowed to leave the open tab behind,
    /// which is the point of the markers: they are there to look at the tabs that are not on screen,
    /// and a strip that snapped back to the open tab every frame could not be scrolled at all.
    /// </summary>
    /// <param name="widths">How wide each tab comes out.</param>
    /// <param name="room">The cells the tabs have, markers already taken off.</param>
    /// <param name="open">Which tab is on screen.</param>
    /// <returns>One past the last tab that is showing.</returns>
    public int Showing(IReadOnlyList<int> widths, int room, int open)
    {
        ArgumentNullException.ThrowIfNull(widths);

        var wanted = Math.Clamp(open, 0, Math.Max(0, widths.Count - 1));

        _first = Math.Clamp(_first, 0, Math.Max(0, widths.Count - 1));

        if (_seen != wanted)
        {
            _seen = wanted;
            _first = Math.Min(_first, wanted);

            while (_first < wanted && Ends(widths, room) <= wanted)
            {
                _first++;
            }
        }

        return Math.Max(Ends(widths, room), _first + 1);
    }

    /// <summary>One past the last tab that fits, counting from the one scrolled to.</summary>
    /// <param name="widths">How wide each tab comes out.</param>
    /// <param name="room">The cells the tabs have.</param>
    /// <returns>Where the showing tabs end.</returns>
    private int Ends(IReadOnlyList<int> widths, int room)
    {
        var taken = 0;
        var last = _first;

        while (last < widths.Count && taken + widths[last] + Between <= room)
        {
            taken += widths[last] + Between;
            last++;
        }

        return last;
    }
}
