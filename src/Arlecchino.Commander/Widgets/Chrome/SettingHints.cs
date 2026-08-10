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
/// The box that stands over the settings line while it is being typed on, holding what the half-typed
/// word could still turn into.
///
/// It grows out of the line rather than floating anywhere: it stands just above the prompt with its
/// left edge lined up with it, so the eye that is on what is being typed does not have to go looking.
/// The row the panel closes itself with is left alone under it, since a box that swallowed that line
/// would read as the panel having come apart. Nothing is dimmed behind it either: it covers a corner of
/// a panel, and only while somebody is typing.
///
/// A row says three different things and has to look like it. Spacing alone read as one sentence —
/// <c>editor nvim the program F4 opens a file in</c> — so the three are divided the way the panel
/// divides what it says about a file: a dot between them, and a weight each. The word to type is the
/// text, what it is now is written the way a path is, and what it is for is quieter than both but still
/// meant to be read.
/// </summary>
internal static class SettingHints
{
    private const string Between = " · ";

    private const int SideRoom = 2;
    private const int MostRows = 8;

    /// <summary>Draws it, or nothing at all when there is nothing left to suggest.</summary>
    /// <param name="over">Everything above the line, which the box places itself at the foot of.</param>
    /// <param name="title">What the box is called.</param>
    /// <param name="rows">What it lists, the best match first.</param>
    public static void Draw(SurfaceRegion over, string title, IReadOnlyList<SettingHint> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0 || over.Height < 4)
        {
            return;
        }

        var showing = Math.Min(rows.Count, Math.Min(MostRows, over.Height - 3));
        var words = 0;
        var values = 0;
        var about = 0;

        for (var at = 0; at < showing; at++)
        {
            words = Math.Max(words, TextWidth.Of(rows[at].Word));
            values = Math.Max(values, TextWidth.Of(rows[at].Value));
            about = Math.Max(about, TextWidth.Of(rows[at].About));
        }

        var wanted = Math.Max(TextWidth.Of(title) + 2, words + Column(values) + Column(about));
        var inner = Math.Min(wanted, over.Width - (SideRoom * 2) - 4);

        Paint(over, title, rows, showing, new(words, values, inner));
    }

    /// <summary>How much room a column takes once the divider in front of it is counted.</summary>
    /// <param name="width">How wide the column itself is, or zero when there is nothing in it.</param>
    /// <returns>The cells it costs.</returns>
    private static int Column(int width) => width == 0 ? 0 : width + Between.Length;

    /// <summary>Draws the box itself at the foot of the region it was given.</summary>
    /// <param name="over">Everything above the line.</param>
    /// <param name="title">What the box is called.</param>
    /// <param name="rows">What it lists.</param>
    /// <param name="showing">How many of them fit.</param>
    /// <param name="columns">Where each column starts and how wide the box is inside.</param>
    private static void Paint(
        SurfaceRegion over,
        string title,
        IReadOnlyList<SettingHint> rows,
        int showing,
        Columns columns)
    {
        var inner = columns.Inner;

        if (inner <= 0)
        {
            return;
        }

        var coat = Skin.Overlay;
        var width = inner + 4;
        var height = showing + 2;
        var box = over
            .Rows(over.Height - height - 1, height)
            .Inset(new Margin(SideRoom, 0, Math.Max(0, over.Width - width - SideRoom), 0));

        box.Fill(coat.Text);
        box.Write(0, 0, $"╭─ {title} {new('─', Math.Max(0, inner - TextWidth.Of(title) - 1))}╮", coat.Faded);

        for (var at = 0; at < showing; at++)
        {
            box.Write(at + 1, 0, "│", coat.Faded);
            box.Write(at + 1, width - 1, "│", coat.Faded);

            Row(box.Rows(at + 1, 1).Inset(new Margin(2, 0, 2, 0)), rows[at], columns);
        }

        box.Write(showing + 1, 0, $"╰{new string('─', inner + 2)}╯", coat.Faded);
    }

    /// <summary>
    /// One row, written a column at a time so each of the three carries its own color. A column with
    /// nothing in it anywhere — the suggestions have neither a value nor a description — takes no room
    /// and no divider, so a list of editors is a list of editors rather than a table of blanks.
    /// </summary>
    /// <param name="row">The row to draw on, already inside the borders.</param>
    /// <param name="hint">What it says.</param>
    /// <param name="columns">Where each column starts.</param>
    private static void Row(SurfaceRegion row, SettingHint hint, Columns columns)
    {
        var coat = Skin.Overlay;

        row.Write(0, 0, TextWidth.Truncate(hint.Word, Math.Min(columns.Word, columns.Inner)), coat.Text);

        if (columns.Values > 0 && columns.Value < columns.Inner)
        {
            row.Write(0, columns.Word, Between, coat.Trace);
            row.Write(0, columns.Value, TextWidth.Truncate(hint.Value, columns.Values), coat.Remote);
        }

        var left = columns.Inner - columns.About;

        if (hint.About.Length == 0 || left <= 0)
        {
            return;
        }

        row.Write(0, columns.About - Between.Length, Between, coat.Trace);
        row.Write(0, columns.About, TextWidth.Truncate(hint.About, left), coat.Second);
    }

    /// <summary>
    /// Where the three columns of a row start, counted from inside the border, and how wide the box is
    /// inside. Worked out once for the whole box: the point of the columns is that the rows line up.
    /// </summary>
    /// <param name="Word">How wide the first column is, which is where the divider after it goes.</param>
    /// <param name="Values">How wide the second is, or zero when no row has one.</param>
    /// <param name="Inner">How wide the box is inside its borders.</param>
    private readonly record struct Columns(int Word, int Values, int Inner)
    {
        /// <summary>Where what a setting is now starts.</summary>
        public int Value => Word + Between.Length;

        /// <summary>Where what it is for starts.</summary>
        public int About => Word + Column(Values) + Between.Length;
    }
}
