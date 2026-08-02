using System;
using Arlecchino.Commander.Stores;
using Arlecchino.Rendering;

using Arlecchino.Commander.Widgets.Panels;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// The column between the panels. It is not decoration: it says which way the work goes and what is
/// waiting to be worked on, written down the column a letter to a row.
/// </summary>
public sealed class Gutter
{
    /// <summary>How wide it is.</summary>
    public const int Width = 3;

    private const int Least = 4;

    private readonly Sessions _sessions;
    private readonly Pair _panels;

    /// <summary>Draws the column between two panels.</summary>
    /// <param name="sessions">Which side is being worked in.</param>
    /// <param name="panels">The two panels on screen.</param>
    public Gutter(Sessions sessions, Pair panels)
    {
        _sessions = sessions;
        _panels = panels;
    }

    /// <summary>Draws it.</summary>
    /// <param name="gutter">The column to draw on.</param>
    public void Draw(SurfaceRegion gutter)
    {
        var coat = Skin.Terminal;

        gutter.Fill(coat.Text);

        if (gutter.Height < Least || gutter.Width < Width)
        {
            return;
        }

        var marks = _panels.Active.State.Marks.Count;
        var label = marks > 0 ? Loc(LocString.GutterMarked, marks) : Loc(LocString.GutterIdle);
        var style = marks > 0 ? coat.Accent : coat.Sleeping;
        var top = Math.Max(0, (gutter.Height - label.Length - 2) / 2);

        gutter.Write(top, 1, _sessions.RightIsActive.Value ? "←" : "→", style);

        for (var index = 0; index < label.Length && top + index + 2 < gutter.Height; index++)
        {
            gutter.Write(top + index + 2, 1, label[index].ToString(), style);
        }
    }
}
