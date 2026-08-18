using System;
using System.Collections.Generic;
using Arlecchino.Commander.Model;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Colors;
using Arlecchino.Rendering.Text;
using Arlecchino.Commander.Widgets.Chrome;

namespace Arlecchino.Commander.Widgets.Dialogs;

/// <summary>
/// Where a question ended up on screen, so a click can be told what it landed on. The box is as tall as
/// what is written in it and sits a third of the way down.
/// </summary>
/// <param name="Box">The whole box, for telling a click on it from a click outside.</param>
/// <param name="Confirm">The button that goes ahead.</param>
/// <param name="Cancel">The button that does not.</param>
/// <param name="Options">The switches, one row each, or empty when there are none.</param>
public readonly record struct OperationSpots(
    SurfaceRegion Box,
    SurfaceRegion Confirm,
    SurfaceRegion Cancel,
    SurfaceRegion Options);

/// <summary>
/// Draws an <see cref="Operation"/>. One shell serves all of them, so what changes between copying and
/// deleting is the words and the tone — never the shape, and never where to look for the button.
/// </summary>
public static class OperationBox
{
    private const int Width = 62;
    private const int Padding = 2;
    private const int MostItems = 5;
    private const int MostLines = 3;

    /// <summary>Draws the dialog over whatever is behind it.</summary>
    /// <param name="screen">The whole screen.</param>
    /// <param name="operation">What is being asked.</param>
    /// <returns>Where it landed, for the clicks.</returns>
    public static OperationSpots Draw(SurfaceRegion screen, Operation operation)
    {
        var width = Math.Min(Width, screen.Width - 4);
        var rows = Rows(operation);

        if (width < 30 || screen.Height < rows + 2)
        {
            return default;
        }

        var top = Math.Max(0, (screen.Height - rows) / 3);
        var left = (screen.Width - width) / 2;
        var box = screen.Rows(top, rows).Inset(new Margin(left, 0, screen.Width - width - left, 0));
        var coat = Skin.Overlay;
        var (fill, on, band) = operation.Tone();

        box.Fill(coat.Text);
        box.Rows(0, 1).Fill(Skin.Paint(fill, fill));

        var content = box.Rows(1, rows - 1).Inset(new Margin(Padding, 0, Padding, 0));
        var row = 1;

        content.Write(row, 0, operation.Title, coat.Strong);
        content.WriteLine(row, operation.Key, Skin.Paint(fill, Skin.OverlayInk), Align.Right);

        if (operation.Subtitle.Length > 0)
        {
            content.Write(row + 1, 0, TextWidth.Truncate(operation.Subtitle, content.Width), coat.Hint);
            row++;
        }

        row += 2;
        row = What(content, operation, coat, row);
        row = OperationField.Draw(content, operation, coat, fill, row);

        var switches = operation.Options.Count == 0
            ? default
            : content.Rows(row, operation.Options.Count);

        row = Options(content, operation, coat, fill, row);

        Note(content, operation, band, row);

        var (confirm, cancel) = Buttons(content, operation, coat, fill, on, row + Lines(operation));

        return new(box, confirm, cancel, switches);
    }

    /// <summary>
    /// How tall the dialog comes out, which follows from what it is asking. Worked out the same way the
    /// drawing works it out, so the box is never taller than what is written in it.
    /// </summary>
    /// <param name="operation">What is being asked.</param>
    /// <returns>The rows it needs.</returns>
    private static int Rows(Operation operation)
    {
        var rows = operation.Subtitle.Length > 0 ? 5 : 4;

        if (operation.Items.Count > 0)
        {
            rows += 2 + Math.Min(MostItems, operation.Items.Count);
        }

        if (operation.FieldLabel is not null)
        {
            rows += 3;
        }

        if (operation.Options.Count > 0)
        {
            rows += operation.Options.Count + 1;
        }

        return rows + Lines(operation) + 3;
    }

    /// <summary>How many rows the note takes, and none when there is no note.</summary>
    /// <param name="operation">What is being asked.</param>
    /// <returns>The rows.</returns>
    private static int Lines(Operation operation) => Said(operation) is { } label
        ? Math.Min(MostLines, Wrapped(label.Text, Width - (Padding * 2) - 4).Count) + 1
        : 0;

    /// <summary>What the note says, if it says anything at all just now.</summary>
    /// <param name="operation">What is being asked.</param>
    /// <returns>The remark, or nothing.</returns>
    private static Remark? Said(Operation operation) => operation.Note?.Invoke(operation);

    private static int What(SurfaceRegion content, Operation operation, Skin.Coat coat, int row)
    {
        if (operation.Items.Count == 0)
        {
            return row;
        }

        content.Write(row, 0, operation.ItemsLabel.ToUpperInvariant(), coat.Label);

        var showing = Math.Min(MostItems, operation.Items.Count);

        for (var index = 0; index < showing; index++)
        {
            var entry = operation.Items[index];
            var meta = entry.IsFolder ? Loc(LocString.KindFolder) : Sizes.Brief(entry.Size);

            content.Write(row + 1 + index, 0, Kinds.Tag(entry), coat.Hint);
            content.Write(row + 1 + index,
                Kinds.TagWidth,
                TextWidth.Truncate(entry.Name, content.Width - Kinds.TagWidth - 12),
                coat.Text);
            content.WriteLine(row + 1 + index, meta, coat.Label, Align.Right);
        }

        if (operation.Items.Count > showing)
        {
            content.WriteLine(row + showing, Loc(LocString.OperationAndMore, operation.Items.Count - showing), coat.Label, Align.Right);
        }

        return row + showing + 2;
    }

    private static int Options(SurfaceRegion content, Operation operation, Skin.Coat coat, Rgb fill, int row)
    {
        for (var index = 0; index < operation.Options.Count; index++)
        {
            var option = operation.Options[index];
            var here = operation.ChosenIndex == index;

            content.Write(row + index,
                0,
                option.On ? "[×]" : "[ ]",
                option.On ? Skin.Paint(fill, Skin.OverlayInk) : coat.Label);

            content.Write(row + index, 4, option.Label, here ? coat.Strong : coat.Second);

            if (here)
            {
                content.Write(row + index, content.Width - 6, Loc(LocString.OperationSpace), coat.Ghost);
            }
        }

        return operation.Options.Count == 0 ? row : row + operation.Options.Count + 1;
    }

    /// <summary>
    /// The band that says what will actually happen. It is the one section no operation may leave out:
    /// the point of asking at all is that the answer is not obvious from the verb.
    /// </summary>
    /// <param name="content">Where to draw.</param>
    /// <param name="operation">What is being asked.</param>
    /// <param name="band">The tinted background it sits on.</param>
    /// <param name="row">Which row to start at.</param>
    private static void Note(SurfaceRegion content, Operation operation, Rgb band, int row)
    {
        if (Said(operation) is not { } label)
        {
            return;
        }

        var lines = Wrapped(label.Text, content.Width - 4);
        var text = Skin.Paint(Skin.Bone, band);

        for (var index = 0; index < Math.Min(MostLines, lines.Count) && row + index < content.Height; index++)
        {
            content.Rows(row + index, 1).Fill(text);
            content.Write(row + index, 2, lines[index], text);
        }

        content.Write(row, 0, label.Warns ? "!" : "i", Skin.Paint(label.Warns ? Skin.Amber : Skin.Sea, band));
    }

    /// <summary>Draws the two buttons and says where they went, so a click can find them.</summary>
    /// <param name="content">Where to draw.</param>
    /// <param name="operation">What is being asked.</param>
    /// <param name="coat">The surface underneath.</param>
    /// <param name="fill">The color of the operation.</param>
    /// <param name="on">What is written on that color.</param>
    /// <param name="row">Which row they go on.</param>
    /// <returns>The two buttons.</returns>
    private static (SurfaceRegion Confirm, SurfaceRegion Cancel) Buttons(
        SurfaceRegion content,
        Operation operation,
        Skin.Coat coat,
        Rgb fill,
        Rgb on,
        int row)
    {
        if (row >= content.Height)
        {
            return default;
        }

        var go = "  " + Loc(LocString.OperationConfirm, operation.Verb) + "  ";
        var no = "  " + Loc(LocString.OperationCancel) + "  ";

        content.Write(row, 0, go, Skin.Paint(on, fill, TextStyle.Bold));
        content.Write(row, go.Length + 2, no, Skin.Paint(Skin.Secondary, Skin.Chip));

        var tab = operation.Target is not null
            ? Loc(LocString.OperationTabCompletes)
            : operation.Options.Count > 0
                ? Loc(LocString.OperationTabSwitches)
                : "";

        if (tab.Length > 0)
        {
            content.WriteLine(row, tab, coat.Label, Align.Right);
        }

        var top = content.Top + row;

        return (
            new(content.Surface, content.Left, top, go.Length, 1),
            new(content.Surface, content.Left + go.Length + 2, top, no.Length, 1));
    }

    /// <summary>Breaks a sentence over lines without breaking a word.</summary>
    /// <param name="text">What to break.</param>
    /// <param name="room">How wide a line may be.</param>
    /// <returns>The lines.</returns>
    private static List<string> Wrapped(string text, int room)
    {
        var lines = new List<string>();
        var line = "";

        foreach (var word in text.Split(' '))
        {
            if (line.Length > 0 && line.Length + word.Length + 1 > room)
            {
                lines.Add(line);
                line = word;

                continue;
            }

            line = line.Length == 0 ? word : $"{line} {word}";
        }

        if (line.Length > 0)
        {
            lines.Add(line);
        }

        return lines;
    }
}
