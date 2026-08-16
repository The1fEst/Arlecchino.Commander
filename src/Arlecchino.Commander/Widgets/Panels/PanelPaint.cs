using System;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Input;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Text;
using Arlecchino.Widgets.Text;

namespace Arlecchino.Commander.Widgets.Panels;

/// <summary>
///     What a panel looks like: the rule down its edge, the trail along the top, the column heads, and the
///     band saying what the cursor is on. Everything here reads the panel and writes nothing back.
/// </summary>
public sealed class PanelPaint
{
    private const int SideRoom = 1;
    private const int Chrome = 6;
    private const int HeadRow = 2;
    private const int ListRow = HeadRow + 2;

    private SurfaceRegion _heads;

    /// <summary>Whether the folder is still being read.</summary>
    public bool Loading { get; set; }

    /// <summary>What the source said instead of a listing, or nothing when it gave one.</summary>
    public string Error { get; set; } = "";

    /// <summary>How much room the volume has left, or nothing when it would not say.</summary>
    public string Free { get; set; } = "";

    /// <summary>
    ///     Draws everything but the files themselves.
    /// </summary>
    /// <param name="region">The whole panel, rule included.</param>
    /// <param name="panel">The panel being drawn.</param>
    /// <returns>Where the rows go, which is empty when there is no room for any or nothing to put there.</returns>
    public SurfaceRegion Draw(SurfaceRegion region, FilePanel panel)
    {
        var none = region.Rows(region.Height, 0);

        if (region.IsEmpty)
        {
            return none;
        }

        var coat = panel.IsFocused ? Skin.Lively : Skin.Quiet;
        var color = panel.IsFocused ? Skin.BorderActiveColor : Skin.BorderInactiveColor;

        region.Fill(coat.Text);

        var (edge, rest) = region.SplitLeft(1);

        edge.Fill(panel.IsFocused ? Skin.CrimsonFill : coat.Text);
        rest.Rows(0, 1).Fill(color, '▀');
        rest.Rows(rest.Height - 1, 1).Fill(color, '▄');

        var body = rest.Inset(SideRoom);
        if (body.IsEmpty)
        {
            return none;
        }

        Breadcrumb.Draw(body.Rows(HeadRow - 2, 1), panel.State, coat, PanelRow.Under(coat), Tally(panel));

        if (body.Height <= Chrome)
        {
            return none;
        }

        body.Rows(HeadRow - 1, 1).Fill(coat.Rule, '─');

        _heads = body.Rows(HeadRow, 1);

        Heads(_heads, panel.State, coat);

        body.Rows(HeadRow + 1, 1).Fill(coat.Rule, '─');
        Foot(body.Rows(body.Height - 1, 1), panel, coat);

        if (Error.Length == 0)
        {
            return body.Rows(ListRow, body.Height - Chrome);
        }

        body.Rows(ListRow, 1)
            .WriteLine(
                0,
                TextWidth.Truncate(Error, body.Width),
                Skin.Paint(Skin.Flame, PanelRow.Under(coat)));

        return none;
    }

    /// <summary>Which column head a click landed on.</summary>
    /// <param name="mouse">The event that arrived.</param>
    /// <returns>What it sorts by, or nothing when it missed the heads.</returns>
    public Sorting? Hit(MouseEvent mouse)
    {
        return mouse.Action == MouseAction.Pressed && _heads.Contains(mouse.Row, mouse.Column)
            ? PanelColumns.Hit(_heads.ToLocal(mouse.Row, mouse.Column).Column, _heads.Width)
            : null;
    }

    /// <summary>The column heads, in small capitals, with the sort arrow on the one that is sorted by.</summary>
    /// <param name="row">The row to draw on.</param>
    /// <param name="state">What the panel is showing.</param>
    /// <param name="coat">The surface underneath.</param>
    private static void Heads(SurfaceRegion row, PanelState state, Skin.Coat coat)
    {
        var (name, size, date) = PanelColumns.Widths(row.Width);

        Head(row, 0, Kinds.TagWidth - 1, "", Sorting.Kind, state, coat, Align.Left);
        Head(row, Kinds.TagWidth, name, Loc(LocString.PanelName), Sorting.Name, state, coat, Align.Left);

        if (size > 0)
        {
            Head(row,
                Kinds.TagWidth + name + PanelColumns.Gap,
                size,
                Loc(LocString.PanelSize),
                Sorting.Size,
                state,
                coat,
                Align.Right);
        }

        if (date > 0)
        {
            Head(row,
                Kinds.TagWidth + name + PanelColumns.Gap + size + PanelColumns.Gap,
                date,
                Loc(LocString.PanelModified),
                Sorting.Modified,
                state,
                coat,
                Align.Right);
        }
    }

    /// <summary>One column head.</summary>
    /// <param name="row">The row to draw on.</param>
    /// <param name="column">Where the column starts.</param>
    /// <param name="width">How wide it is.</param>
    /// <param name="text">What it is called.</param>
    /// <param name="sorting">What clicking it sorts by.</param>
    /// <param name="state">What the panel is showing, for what it is sorted by now.</param>
    /// <param name="coat">The surface underneath.</param>
    /// <param name="align">Which end of the column the head sits at.</param>
    private static void Head(
        SurfaceRegion row,
        int column,
        int width,
        string text,
        Sorting sorting,
        PanelState state,
        Skin.Coat coat,
        Align align)
    {
        var sorted = state.Sorting == sorting;
        var arrow = state.Descending ? " ↓" : " ↑";
        var whole = sorted ? text + arrow : text;
        var at = align == Align.Right ? column + width - TextWidth.Of(whole) : column;

        if (at < column || width <= 0)
        {
            return;
        }

        row.Write(0, at, text, coat.Label);

        if (sorted)
        {
            row.Write(0, at + TextWidth.Of(text), arrow, coat.Accent);
        }
    }

    /// <summary>How much is in the folder, said at the right of the trail.</summary>
    /// <param name="panel">The panel being drawn.</param>
    /// <returns>The words.</returns>
    private string Tally(FilePanel panel)
    {
        if (Loading)
        {
            return Loc(LocString.PanelReading);
        }

        var entries = panel.Entries;
        var items = entries.Count > 0 && entries[0].IsParent ? entries.Count - 1 : entries.Count;
        var counted = items == 1 ? Loc(LocString.OneItem) : Loc(LocString.ManyItems, items);

        return Free.Length == 0 ? counted : Loc(LocString.Joined, counted, Free);
    }

    /// <summary>
    ///     The foot: what the cursor is on, or what has been marked. Marking turns the whole band the
    ///     color of a mark, so the panel says it is holding something without anything having to be read.
    /// </summary>
    /// <param name="row">The row to draw on.</param>
    /// <param name="panel">The panel being drawn.</param>
    /// <param name="coat">The surface underneath.</param>
    private void Foot(SurfaceRegion row, FilePanel panel, Skin.Coat coat)
    {
        if (panel.IsSearching)
        {
            var label = Loc(LocString.PanelJumpTo, "");

            row.Write(0, 0, TextWidth.Truncate(label, row.Width), coat.Accent);
            EntryRow.Draw(
                row,
                0,
                TextWidth.Of(label),
                Math.Max(0, row.Width - TextWidth.Of(label)),
                panel.Typed,
                Skin.Typed(coat.Accent, Skin.Crimson));

            return;
        }

        if (panel.State.Marks.Count > 0)
        {
            Held(row, panel, coat);

            return;
        }

        if (Loading)
        {
            row.Write(0, 0, Loc(LocString.PanelReadingFolder), coat.Faded);

            return;
        }

        var said = panel.Current is { } current ? Describe(current) : Loc(LocString.PanelNothingHere);

        row.Write(0, 0, TextWidth.Truncate(said, row.Width), coat.Meta);
    }

    /// <summary>The foot while something is marked.</summary>
    /// <param name="row">The row to draw on.</param>
    /// <param name="panel">The panel being drawn.</param>
    /// <param name="coat">The surface underneath.</param>
    private static void Held(SurfaceRegion row, FilePanel panel, Skin.Coat coat)
    {
        var bytes = 0L;

        foreach (var entry in panel.Targets())
        {
            bytes += entry.Size;
        }

        var marks = panel.State.Marks.Count;
        var held = bytes > 0
            ? Loc(LocString.PanelMarkedSize, marks, Sizes.Brief(bytes))
            : Loc(LocString.Marked, marks);

        row.Fill(coat.MarkedRow);
        row.Write(0, 0, TextWidth.Truncate(held, row.Width), coat.Marked);
        row.WriteLine(0, Loc(LocString.PanelMarkHints), coat.MarkedMeta, Align.Right);
    }

    /// <summary>What one file is, for the foot.</summary>
    /// <param name="entry">The file.</param>
    /// <returns>The words.</returns>
    private static string Describe(FileEntry entry)
    {
        var what = entry.IsFolder ? Loc(LocString.KindFolder) : Sizes.Brief(entry.Size);
        var said = Loc(LocString.Joined, entry.Name, what);

        return entry.IsReadOnly ? Loc(LocString.PanelReadOnly, entry.Name, what) : said;
    }
}
