using System;
using System.Collections.Generic;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// Which of the tabs are showing, when there is no room for all of them. It holds the one piece of state
/// the band has, which is how far it has been scrolled.
/// </summary>
public sealed class TabWindow
{
    /// <summary>The gap kept between one tab and the next.</summary>
    public const int Gap = 1;

    private int _first;
    private int _width = -1;

    /// <summary>The first tab showing.</summary>
    public int First => _first;

    /// <summary>Scrolls it, for a click on one of the markers.</summary>
    /// <param name="by">Which way, and how far.</param>
    /// <param name="count">How many tabs there are.</param>
    public void Scroll(int by, int count) => _first = Math.Clamp(_first + by, 0, Math.Max(0, count - 1));

    /// <summary>
    /// Works out what is showing. Going to a tab brings it into view however far the strip had been
    /// scrolled, while scrolling with the markers is allowed to leave the open tab behind.
    /// </summary>
    /// <param name="widths">How wide each tab comes out.</param>
    /// <param name="room">The cells the tabs have, markers already taken off.</param>
    /// <param name="open">Which tab the band is showing.</param>
    /// <returns>One past the last tab that is showing.</returns>
    public int Showing(IReadOnlyList<int> widths, int room, int open)
    {
        var target = Math.Clamp(open, 0, Math.Max(0, widths.Count - 1));

        _first = Math.Clamp(_first, 0, Math.Max(0, widths.Count - 1));

        if (_width == target)
        {
            return Math.Max(Ends(widths, room), _first + 1);
        }

        _width = target;
        _first = Math.Min(_first, target);

        while (_first < target && Ends(widths, room) <= target)
        {
            _first++;
        }

        return Math.Max(Ends(widths, room), _first + 1);
    }

    /// <summary>One past the last tab that fits, counting from the one scrolled to.</summary>
    /// <param name="widths">How wide each tab comes out.</param>
    /// <param name="room">The cells the tabs have.</param>
    /// <returns>Where the showing tabs end.</returns>
    private int Ends(IReadOnlyList<int> widths, int room)
    {
        var width = 0;
        var last = _first;

        while (last < widths.Count && width + widths[last] + Gap <= room)
        {
            width += widths[last] + Gap;
            last++;
        }

        return last;
    }
}
