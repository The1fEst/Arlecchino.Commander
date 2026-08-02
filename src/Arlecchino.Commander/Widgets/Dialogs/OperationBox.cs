using System;
using System.Collections.Generic;
using Arlecchino.Commander.Model;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Colors;
using Arlecchino.Rendering.Text;
using Arlecchino.Commander.Widgets.Chrome;

namespace Arlecchino.Commander.Widgets.Dialogs;

/// <summary>
/// Draws an <see cref="Operation"/>. One shell serves all of them, so what changes between copying and
/// deleting is the words and the tone — never the shape, and never where to look for the button.
/// </summary>
public static class OperationBox
{
    private const int Wanted = 62;
    private const int Padding = 2;
    private const int MostItems = 5;
    private const int MostLines = 3;

    /// <summary>Draws the dialog over whatever is behind it.</summary>
    /// <param name="screen">The whole screen.</param>
    /// <param name="operation">What is being asked.</param>
    public static void Draw(SurfaceRegion screen, Operation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var width = Math.Min(Wanted, screen.Width - 4);
        var rows = Rows(operation);

        if (width < 30 || screen.Height < rows + 2)
        {
            return;
        }

        var top = Math.Max(0, (screen.Height - rows) / 3);
        var left = (screen.Width - width) / 2;
        var box = screen.Rows(top, rows).Inset(new Margin(left, 0, screen.Width - width - left, 0));
        var coat = Skin.Overlay;
        var (fill, on, band) = operation.Tone();

        box.Fill(coat.Text);
        box.Rows(0, 1).Fill(Skin.Paint(fill, fill));

        var inside = box.Rows(1, rows - 1).Inset(new Margin(Padding, 0, Padding, 0));
        var row = 1;

        inside.Write(row, 0, operation.Title, coat.Strong);
        inside.WriteLine(row, operation.Key, Skin.Paint(fill, Skin.Over), Align.Right);

        if (operation.Subtitle.Length > 0)
        {
            inside.Write(row + 1, 0, TextWidth.Truncate(operation.Subtitle, inside.Width), coat.Faded);
            row++;
        }

        row += 2;
        row = What(inside, operation, coat, row);
        row = Field(inside, operation, coat, fill, row);
        row = Options(inside, operation, coat, fill, row);

        Note(inside, operation, band, row);
        Buttons(inside, operation, coat, fill, on, row + Lines(operation));
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
    private static int Lines(Operation operation) => Said(operation) is { } said
        ? Math.Min(MostLines, Wrapped(said.Text, Wanted - (Padding * 2) - 4).Count) + 1
        : 0;

    /// <summary>What the note says, if it says anything at all just now.</summary>
    /// <param name="operation">What is being asked.</param>
    /// <returns>The remark, or nothing.</returns>
    private static Remark? Said(Operation operation) => operation.Note?.Invoke(operation);

    private static int What(SurfaceRegion inside, Operation operation, Skin.Coat coat, int row)
    {
        if (operation.Items.Count == 0)
        {
            return row;
        }

        inside.Write(row, 0, operation.ItemsLabel.ToUpperInvariant(), coat.Label);

        var shown = Math.Min(MostItems, operation.Items.Count);

        for (var index = 0; index < shown; index++)
        {
            var entry = operation.Items[index];
            var meta = entry.IsFolder ? "folder" : Sizes.Brief(entry.Size);

            inside.Write(row + 1 + index, 0, Kinds.Tag(entry), coat.Faded);
            inside.Write(row + 1 + index, Kinds.TagWidth,
                TextWidth.Truncate(entry.Name, inside.Width - Kinds.TagWidth - 12), coat.Text);
            inside.WriteLine(row + 1 + index, meta, coat.Label, Align.Right);
        }

        if (operation.Items.Count > shown)
        {
            inside.WriteLine(row + shown, Loc(LocString.OperationAndMore, operation.Items.Count - shown), coat.Label, Align.Right);
        }

        return row + shown + 2;
    }

    private static int Field(SurfaceRegion inside, Operation operation, Skin.Coat coat, Rgb fill, int row)
    {
        if (operation.FieldLabel is not { } label)
        {
            return row;
        }

        inside.Write(row, 0, label.ToUpperInvariant(), coat.Label);

        var line = inside.Rows(row + 1, 1);
        var chip = Skin.Inlaid;

        line.Fill(chip.Text);

        var at = 1;

        if (operation.Host.Length > 0)
        {
            line.Write(0, at, operation.Host, chip.Remote);
            at += operation.Host.Length + 1;
        }

        var written = operation.Secret ? new('•', operation.Value.Length) : operation.Value;
        var room = Math.Max(1, line.Width - at - 2);
        var offset = Math.Max(0, operation.Caret - room + 1);
        var shown = written[offset..Math.Min(written.Length, offset + room)];

        line.Write(0, at, shown, chip.Text);

        if (operation.Chosen < 0)
        {
            line.Write(0, at + operation.Caret - offset,
                operation.Caret < operation.Value.Length ? operation.Value[operation.Caret].ToString() : " ",
                Skin.Paint(Skin.Ink, fill));
        }

        if (operation.FieldHint.Length > 0)
        {
            inside.WriteLine(row, TextWidth.Truncate(operation.FieldHint, inside.Width - label.Length - 2),
                coat.Label, Align.Right);
        }

        return row + 3;
    }

    private static int Options(SurfaceRegion inside, Operation operation, Skin.Coat coat, Rgb fill, int row)
    {
        for (var index = 0; index < operation.Options.Count; index++)
        {
            var option = operation.Options[index];
            var here = operation.Chosen == index;

            inside.Write(row + index, 0, option.On ? "[×]" : "[ ]",
                option.On ? Skin.Paint(fill, Skin.Over) : coat.Label);

            inside.Write(row + index, 4, option.Label, here ? coat.Strong : coat.Second);

            if (here)
            {
                inside.Write(row + index, inside.Width - 6, Loc(LocString.OperationSpace), coat.Ghost);
            }
        }

        return operation.Options.Count == 0 ? row : row + operation.Options.Count + 1;
    }

    /// <summary>
    /// The band that says what will actually happen. It is the one section no operation may leave out:
    /// the point of asking at all is that the answer is not obvious from the verb.
    /// </summary>
    /// <param name="inside">Where to draw.</param>
    /// <param name="operation">What is being asked.</param>
    /// <param name="band">The tinted background it sits on.</param>
    /// <param name="row">Which row to start at.</param>
    private static void Note(SurfaceRegion inside, Operation operation, Rgb band, int row)
    {
        if (Said(operation) is not { } said)
        {
            return;
        }

        var lines = Wrapped(said.Text, inside.Width - 4);
        var text = Skin.Paint(Skin.Bone, band);

        for (var index = 0; index < Math.Min(MostLines, lines.Count) && row + index < inside.Height; index++)
        {
            inside.Rows(row + index, 1).Fill(text);
            inside.Write(row + index, 2, lines[index], text);
        }

        inside.Write(row, 0, said.Warns ? "!" : "i", Skin.Paint(said.Warns ? Skin.Amber : Skin.Sea, band));
    }

    private static void Buttons(
        SurfaceRegion inside,
        Operation operation,
        Skin.Coat coat,
        Rgb fill,
        Rgb on,
        int row)
    {
        if (row >= inside.Height)
        {
            return;
        }

        var go = "  " + Loc(LocString.OperationConfirm, operation.Verb) + "  ";

        inside.Write(row, 0, go, Skin.Paint(on, fill, TextStyle.Bold));
        inside.Write(row, go.Length + 2, "  " + Loc(LocString.OperationCancel) + "  ", Skin.Paint(new(0xA7, 0x9F, 0xAE), Skin.Chip));

        var tab = operation.Over is not null
            ? Loc(LocString.OperationTabCompletes)
            : operation.Options.Count > 0
                ? Loc(LocString.OperationTabSwitches)
                : "";

        if (tab.Length > 0)
        {
            inside.WriteLine(row, tab, coat.Label, Align.Right);
        }
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
