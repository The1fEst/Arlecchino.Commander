using System;
using Arlecchino.Commander.Model;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Colors;
using Arlecchino.Rendering.Text;
using Arlecchino.Commander.Widgets.Chrome;

namespace Arlecchino.Commander.Widgets.Panels;

/// <summary>
/// One file, as a row of a panel. Every span carries its own colour, and the row under the cursor
/// lightens all of them at once: a filled row with a faint neutral still on it is the one thing this
/// design cannot have.
/// </summary>
public static class PanelRow
{
    private const double MarkTint = 0.13;

    /// <summary>Draws it.</summary>
    /// <param name="row">The row to draw on.</param>
    /// <param name="entry">What is on it.</param>
    /// <param name="state">What the panel is showing, for what is marked.</param>
    /// <param name="chosen">Whether the cursor is on it.</param>
    /// <param name="focused">Whether the panel is the one being worked in.</param>
    public static void Draw(SurfaceRegion row, FileEntry entry, PanelState state, bool chosen, bool focused)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(state);

        var coat = focused ? Skin.Lively : Skin.Quiet;
        var cursor = chosen && focused;
        var marked = state.Marks.Contains(entry.Name);
        var (name, size, date) = PanelColumns.Widths(row.Width);

        row.Fill(cursor ? Skin.CursorRow
            : chosen ? Skin.Paint(Skin.Bone, Skin.Chip)
            : marked ? coat.MarkedRow
            : coat.Text);

        var tone = Kinds.ToneOf(entry);

        row.Write(0, 0, Kinds.Tag(entry), Tag(tone, cursor, chosen, marked, coat));
        row.Write(0, Kinds.TagWidth, TextWidth.Truncate(entry.Name, name),
            Name(tone, cursor, chosen, marked, coat));

        if (size > 0)
        {
            var what = entry.IsFolder ? Loc(LocString.PanelFolderKind) : Sizes.Brief(entry.Size);

            row.Write(0, Kinds.TagWidth + name + PanelColumns.Gap + size - TextWidth.Of(what), what,
                Quiet(cursor, chosen, marked, coat));
        }

        if (date <= 0)
        {
            return;
        }

        var when = Sizes.When(entry.Modified);

        row.Write(
            0,
            Kinds.TagWidth + name + PanelColumns.Gap + size + PanelColumns.Gap + date - TextWidth.Of(when),
            when,
            cursor ? Skin.CursorDate : chosen || !marked ? coat.Trace : coat.MarkedMeta);
    }

    /// <summary>What the surface under a row is, which the spans on it are mixed against.</summary>
    /// <param name="coat">The panel's surface.</param>
    /// <returns>The colour.</returns>
    public static Rgb Under(Skin.Coat coat) => ReferenceEquals(coat, Skin.Lively) ? Skin.Lit : Skin.Unlit;

    private static TermColor Tag(Tone tone, bool cursor, bool chosen, bool marked, Skin.Coat coat)
    {
        if (cursor)
        {
            return Skin.CursorTag;
        }

        var back = Back(chosen, marked, coat);

        return tone switch
        {
            Tone.Folder => Skin.Paint(Skin.Sea, back),
            Tone.Protected => Skin.Paint(Skin.AmberRule, back),
            Tone.Ignorable => Skin.Paint(new(0x3A, 0x35, 0x3F), back),
            Tone.Parent => Skin.Paint(new(0x4A, 0x45, 0x50), back),
            _ => Skin.Paint(new(0x6E, 0x68, 0x70), back),
        };
    }

    private static TermColor Name(Tone tone, bool cursor, bool chosen, bool marked, Skin.Coat coat)
    {
        if (cursor)
        {
            return Skin.CursorName;
        }

        var back = Back(chosen, marked, coat);

        if (marked)
        {
            return Skin.Paint(Skin.Coral, back);
        }

        return tone switch
        {
            Tone.Protected => Skin.Paint(Skin.Amber, back),
            Tone.Ignorable => Skin.Paint(new(0x57, 0x51, 0x5F), back),
            Tone.Parent => Skin.Paint(new(0x6E, 0x68, 0x70), back),
            _ => Skin.Paint(Skin.Bone, back),
        };
    }

    private static TermColor Quiet(bool cursor, bool chosen, bool marked, Skin.Coat coat) => cursor
        ? Skin.CursorMeta
        : chosen
            ? Skin.Paint(new(0x8A, 0x83, 0x90), Skin.Chip)
            : marked
                ? coat.MarkedMeta
                : coat.Meta;

    private static Rgb Back(bool chosen, bool marked, Skin.Coat coat) => chosen
        ? Skin.Chip
        : marked
            ? Skin.Blend(Skin.Crimson, MarkTint, Under(coat))
            : Under(coat);
}
