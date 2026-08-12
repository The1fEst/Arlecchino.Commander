using System;
using System.Collections.Generic;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Text;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// One row of the box above the settings line: the word to type, what it is now, and what it is for.
/// </summary>
/// <param name="Word">What typing this row would put on the line.</param>
/// <param name="Value">What it is now, or nothing when the row is itself a value.</param>
/// <param name="About">What it is for.</param>
public sealed record SettingHint(string Word, string Value, string About);

/// <summary>
/// What the half-typed word on the settings line could still turn into, drawn in the shape the keys
/// behind a leader are drawn in. A dot and a weight of its own divide the three columns of a row.
/// </summary>
internal static class SettingHints
{
    private const string Between = " · ";

    private const int MostRows = 8;

    /// <summary>Draws it, or nothing at all when there is nothing left to suggest.</summary>
    /// <param name="over">Everything above the line, which the box places itself at the foot of.</param>
    /// <param name="title">What the box is called.</param>
    /// <param name="hints">What it lists, the best match first.</param>
    public static void Draw(SurfaceRegion over, string title, IReadOnlyList<SettingHint> hints)
    {
        ArgumentNullException.ThrowIfNull(hints);

        if (hints.Count == 0)
        {
            return;
        }

        var showing = Math.Min(hints.Count, MostRows);
        var words = 0;
        var values = 0;
        var about = 0;

        for (var at = 0; at < showing; at++)
        {
            words = Math.Max(words, TextWidth.Of(hints[at].Word));
            values = Math.Max(values, TextWidth.Of(hints[at].Value));
            about = Math.Max(about, TextWidth.Of(hints[at].About));
        }

        var columns = new Columns(words, values, words + Column(values) + Column(about));
        var rows = HintBox.Open(over, title, columns.Inner, showing);

        if (rows.IsEmpty)
        {
            return;
        }

        for (var at = 0; at < showing; at++)
        {
            Row(rows, at, hints[at], columns with { Inner = rows.Width });
        }
    }

    /// <summary>How much room a column takes once the divider in front of it is counted.</summary>
    /// <param name="width">How wide the column itself is, or zero when there is nothing in it.</param>
    /// <returns>The cells it costs.</returns>
    private static int Column(int width) => width == 0 ? 0 : width + Between.Length;

    /// <summary>
    /// One row, written a column at a time so each of the three carries its own color. A column empty in
    /// every row takes no room and no divider.
    /// </summary>
    /// <param name="rows">The rows inside the box.</param>
    /// <param name="at">Which row.</param>
    /// <param name="hint">What it says.</param>
    /// <param name="columns">Where each column starts.</param>
    private static void Row(SurfaceRegion rows, int at, SettingHint hint, Columns columns)
    {
        var coat = Skin.Overlay;

        rows.Write(at, 0, TextWidth.Truncate(hint.Word, Math.Min(columns.Word, columns.Inner)), coat.Text);

        if (columns.Values > 0 && columns.Value < columns.Inner)
        {
            rows.Write(at, columns.Word, Between, coat.Trace);
            rows.Write(at, columns.Value, TextWidth.Truncate(hint.Value, columns.Values), coat.Remote);
        }

        var room = columns.Inner - columns.About;

        if (hint.About.Length == 0 || room <= 0)
        {
            return;
        }

        rows.Write(at, columns.About - Between.Length, Between, coat.Trace);
        rows.Write(at, columns.About, TextWidth.Truncate(hint.About, room), coat.Second);
    }

    /// <summary>
    /// Where the three columns of a row start, counted from the left of the rows, and how much room there
    /// is to write in. Worked out once for the whole box, so that the rows line up.
    /// </summary>
    /// <param name="Word">How wide the first column is, which is where the divider after it goes.</param>
    /// <param name="Values">How wide the second is, or zero when no row has one.</param>
    /// <param name="Inner">How much room there is to write in.</param>
    private readonly record struct Columns(int Word, int Values, int Inner)
    {
        /// <summary>Where what a setting is now starts.</summary>
        public int Value => Word + Between.Length;

        /// <summary>Where what it is for starts.</summary>
        public int About => Word + Column(Values) + Between.Length;
    }
}
