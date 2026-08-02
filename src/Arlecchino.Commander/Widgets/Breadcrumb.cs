using System;
using System.Collections.Generic;
using Arlecchino.Commander.Model;
using Arlecchino.Rendering;

namespace Arlecchino.Commander.Widgets;

/// <summary>
/// Where a panel is looking, written as a trail rather than as a path: the separators recede, the
/// folder you are in is the one in bone. A path too long for the room loses its head, since the end
/// of it is the part that says where you are.
/// </summary>
public static class Breadcrumb
{
    private const int Least = 4;

    /// <summary>Draws it, with whatever the panel wants said at the right of the same row.</summary>
    /// <param name="row">The row to draw on.</param>
    /// <param name="state">What the panel is showing.</param>
    /// <param name="coat">The surface underneath.</param>
    /// <param name="beneath">The colour of that surface, for the pieces that set their own.</param>
    /// <param name="right">What goes at the right, which the trail makes room for.</param>
    public static void Draw(SurfaceRegion row, PanelState state, Skin.Coat coat, Rgb beneath, string right)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(coat);
        ArgumentNullException.ThrowIfNull(right);

        var room = Math.Max(0, row.Width - TextWidth.Of(right) - PanelColumns.Gap);
        var trail = Trail(state, coat, beneath);
        var column = 0;

        while (trail.Count > Least && Spans(trail) > room)
        {
            trail.RemoveRange(0, 2);
            trail[0] = ("…", coat.Ghost);
        }

        if (Spans(trail) > room)
        {
            row.Write(0, 0, Paths.Shortened(state.Source, state.Folder, room), coat.Strong);
        }
        else
        {
            foreach (var (text, style) in trail)
            {
                row.Write(0, column, text, style);
                column += TextWidth.Of(text);
            }
        }

        if (right.Length > 0)
        {
            row.WriteLine(0, right, coat.Trace, Align.Right);
        }
    }

    /// <summary>How wide a set of pieces comes out.</summary>
    /// <param name="trail">The pieces.</param>
    /// <returns>The cells they take.</returns>
    private static int Spans(List<(string Text, TermColor Style)> trail)
    {
        var wanted = 0;

        foreach (var (text, _) in trail)
        {
            wanted += TextWidth.Of(text);
        }

        return wanted;
    }

    /// <summary>
    /// The pieces the trail is written from. A server is named first and in its own colour, since which
    /// machine a path is on matters more than any folder in it.
    /// </summary>
    /// <param name="state">What the panel is showing.</param>
    /// <param name="coat">The surface underneath.</param>
    /// <param name="beneath">The colour of that surface.</param>
    /// <returns>The pieces, in the order they are written, in pairs of separator and name.</returns>
    private static List<(string Text, TermColor Style)> Trail(PanelState state, Skin.Coat coat, Rgb beneath)
    {
        var pieces = new List<(string, TermColor)>();
        var folder = Paths.Homed(state.Source, state.Folder);
        var names = folder.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

        if (state.Source.IsRemote)
        {
            pieces.Add((state.Source.Label, Skin.Paint(Skin.Sea, beneath, TextStyle.Bold)));
            pieces.Add((" ", coat.Text));
        }
        else
        {
            pieces.Add((folder.StartsWith('/') ? "/" : "", coat.Ghost));
            pieces.Add(("", coat.Text));
        }

        for (var index = 0; index < names.Length; index++)
        {
            var last = index == names.Length - 1;

            pieces.Add((names[index], last ? coat.Strong : coat.Meta));
            pieces.Add((last ? "" : " / ", coat.Ghost));
        }

        if (names.Length > 0)
        {
            return pieces;
        }

        pieces.Add((folder, coat.Strong));
        pieces.Add(("", coat.Ghost));

        return pieces;
    }
}
