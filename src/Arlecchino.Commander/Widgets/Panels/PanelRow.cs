using Arlecchino.Commander.Model;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Colors;
using Arlecchino.Rendering.Text;
using Arlecchino.Commander.Widgets.Chrome;

namespace Arlecchino.Commander.Widgets.Panels;

/// <summary>
/// One file, as a row of a panel. Every span carries its own color, and the row under the cursor lightens
/// all of them at once rather than leaving a faint neutral on a filled row.
/// </summary>
public static class PanelRow
{

    /// <summary>Draws it.</summary>
    /// <param name="row">The row to draw on.</param>
    /// <param name="entry">What is on it.</param>
    /// <param name="state">What the panel is showing, for what is marked.</param>
    /// <param name="chosen">Whether the cursor is on it.</param>
    /// <param name="focused">Whether the panel is the one being worked in.</param>
    public static void Draw(SurfaceRegion row, FileEntry entry, PanelState state, bool chosen, bool focused)
    {
        var coat = focused ? Skin.Lively : Skin.Quiet;
        var cursor = chosen && focused;
        var marks = state.Marks.Contains(entry.Name);
        var (name, size, date) = PanelColumns.Widths(row.Width);

        row.Fill(cursor ? Skin.CursorRow
            : chosen ? Skin.On(Skin.Chip).Text
            : marks ? coat.MarkRow
            : coat.Text);

        var tone = Kinds.ToneOf(entry);

        row.Write(0, 0, Kinds.Tag(entry), Tag(tone, cursor, chosen, marks, coat));
        row.Write(0,
            Kinds.TagWidth,
            TextWidth.Truncate(entry.Name, name),
            Name(tone, cursor, chosen, marks, coat));

        if (size > 0)
        {
            var caption = entry.IsFolder ? Loc(LocString.PanelFolderKind) : Sizes.Brief(entry.Size);

            row.Write(0,
                Kinds.TagWidth + name + PanelColumns.Gap + size - TextWidth.Of(caption),
                caption,
                Quiet(cursor, chosen, marks, coat));
        }

        if (date <= 0)
        {
            return;
        }

        var stamp = Sizes.When(entry.Modified);

        row.Write(
            0,
            Kinds.TagWidth + name + PanelColumns.Gap + size + PanelColumns.Gap + date - TextWidth.Of(stamp),
            stamp,
            cursor ? Skin.CursorDate : chosen || !marks ? coat.Trace : coat.MarkMeta);
    }

    /// <summary>What the surface under a row is, which the spans on it are mixed against.</summary>
    /// <param name="coat">The panel's surface.</param>
    /// <returns>The color.</returns>
    public static Rgb Under(Skin.Coat coat) => ReferenceEquals(coat, Skin.Lively) ? Skin.LitInk : Skin.UnlitInk;

    private static TermColor Tag(Tone tone, bool cursor, bool chosen, bool marks, Skin.Coat coat)
    {
        if (cursor)
        {
            return Skin.CursorTag;
        }

        var coating = Skin.On(Back(chosen, marks, coat));

        return tone switch
        {
            Tone.Folder => coating.Remote,
            Tone.Protected => coating.Rule,
            Tone.Ignorable => coating.Ghost,
            Tone.Parent => coating.Trace,
            _ => coating.Hint,
        };
    }

    private static TermColor Name(Tone tone, bool cursor, bool chosen, bool marks, Skin.Coat coat)
    {
        if (cursor)
        {
            return Skin.CursorName;
        }

        if (marks)
        {
            return coat.MarkName;
        }

        var coating = Skin.On(Back(chosen, marks, coat));

        return tone switch
        {
            Tone.Protected => coating.Warning,
            Tone.Ignorable => coating.Label,
            Tone.Parent => coating.Hint,
            _ => coating.Text,
        };
    }

    private static TermColor Quiet(bool cursor, bool chosen, bool marks, Skin.Coat coat) => cursor
        ? Skin.CursorMeta
        : chosen
            ? Skin.On(Skin.Chip).Second
            : marks
                ? coat.MarkMeta
                : coat.Meta;

    private static Rgb Back(bool chosen, bool marks, Skin.Coat coat) => chosen
        ? Skin.Chip
        : marks
            ? coat.Band
            : Under(coat);
}
