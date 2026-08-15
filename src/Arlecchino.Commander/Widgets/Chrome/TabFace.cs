using System;
using Arlecchino.Commander.Model;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Colors;
using Arlecchino.Rendering.Text;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// What one tab looks like, and how wide that comes out. The measuring and the drawing answer the same
/// question, so they are kept together rather than worked out twice.
/// </summary>
public static class TabFace
{
    /// <summary>What a tab costs besides its name: the lit dot, the two edges and the space they sit in.</summary>
    public const int Chrome = 5;

    /// <summary>What the cross costs on top of that: a space to keep it off the name, and itself.</summary>
    public const int Crossed = 2;

    /// <summary>What a name may take when there is room for every tab to be written out in full.</summary>
    public const int Whole = -1;

    /// <summary>
    /// The narrowest a name is shortened to: four cells a side, which is three letters and an ellipsis.
    /// Below this a tab stops naming what it is on, and the strip scrolls instead.
    /// </summary>
    public const int Least = 11;

    private const int Row = 0;

    /// <summary>How wide a tab comes out, once its name has been shortened to what it may take.</summary>
    /// <param name="session">The tab.</param>
    /// <param name="most">The widest a name may be.</param>
    /// <param name="closable">Whether it wears a cross.</param>
    /// <returns>The cells it takes.</returns>
    public static int Width(Session session, int most, bool closable)
    {
        var (near, far) = Shortened(session, most);

        return near.Length + far.Length + 3 + Chrome + (closable ? Crossed : 0);
    }

    /// <summary>Draws one tab.</summary>
    /// <param name="strip">Where to draw.</param>
    /// <param name="column">Where the tab goes.</param>
    /// <param name="session">The tab.</param>
    /// <param name="most">The widest a name may be.</param>
    /// <param name="look">How it is to be drawn.</param>
    /// <returns>Where the name ends, which is where the cross would go.</returns>
    public static int Draw(SurfaceRegion strip, int column, Session session, int most, TabLook look)
    {
        var (near, far) = Shortened(session, most);
        var label = near.Length + far.Length + 3;
        var width = label + Chrome + (look.Closable ? Crossed : 0);
        var under = look.Live ? Skin.Chip : Skin.Lit;
        var lit = new Skin.Coat(under);

        strip.Write(Row, column, new(' ', width), Skin.Paint(Skin.Bone, under));
        Sides(strip, column + 1, session, near, far, look, lit);

        if (look.Closable)
        {
            strip.Write(Row, column + label + 4, "×", look.Live ? lit.Text : lit.Trace);
        }

        return label;
    }

    /// <summary>
    /// The two sides of a tab, with the dot against whichever of them is being worked in. A side on a
    /// server is named after it, in the color servers get.
    /// </summary>
    /// <param name="strip">Where to draw.</param>
    /// <param name="column">Where the tab's text starts.</param>
    /// <param name="session">The tab.</param>
    /// <param name="near">What the left side is called, as it is to be written.</param>
    /// <param name="far">The same for the right.</param>
    /// <param name="look">How it is to be drawn.</param>
    /// <param name="lit">The surface of the tab.</param>
    private static void Sides(
        SurfaceRegion strip,
        int column,
        Session session,
        string near,
        string far,
        TabLook look,
        Skin.Coat lit)
    {
        var dot = look.Live ? lit.Accent : lit.Trace;
        var at = column;

        if (!look.Right)
        {
            strip.Write(Row, at, "●", dot);
            at += 2;
        }

        strip.Write(Row, at, near, Named(session.Left, look is { Live: true, Right: false }, lit));
        at += near.Length + 1;

        strip.Write(Row, at, "⇄", lit.Trace);
        at += 2;

        if (look.Right)
        {
            strip.Write(Row, at, "●", dot);
            at += 2;
        }

        strip.Write(Row, at, far, Named(session.Right, look is { Live: true, Right: true }, lit));
    }

    /// <summary>
    /// The two sides of a tab as they are to be written, each cut to half of what the name may take. Both
    /// sides are cut rather than one, since a tab says what it is by naming both.
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

    /// <summary>What color one side of a tab is written in.</summary>
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

/// <summary>How a tab is to be drawn.</summary>
/// <param name="Live">Whether it is the tab on screen.</param>
/// <param name="Right">Whether the side being worked in is the right one.</param>
/// <param name="Closable">Whether it wears a cross, which the last tab left does not.</param>
public readonly record struct TabLook(bool Live, bool Right, bool Closable);
