using System;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Editing;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Colors;
using Arlecchino.Rendering.Text;
using Arlecchino.Widgets.Text;

namespace Arlecchino.Commander.Widgets.Dialogs;

/// <summary>
/// The one field an operation asks about, drawn the way the framework draws every other line of text. A
/// secret is drawn as dots, its caret and selection counted in the dots rather than in what was typed.
/// </summary>
internal static class OperationField
{
    /// <summary>Draws the label, the host in front of the field, and the field itself.</summary>
    /// <param name="content">The region inside the box.</param>
    /// <param name="operation">What is being asked.</param>
    /// <param name="coat">The colors the box is written in.</param>
    /// <param name="fill">The color of the operation, which the caret stands on.</param>
    /// <param name="row">The row the label goes on.</param>
    /// <returns>The row after the field.</returns>
    public static int Draw(SurfaceRegion content, Operation operation, Skin.Coat coat, Rgb fill, int row)
    {
        if (operation.FieldLabel is not { } label)
        {
            return row;
        }

        content.Write(row, 0, label.ToUpperInvariant(), coat.Label);

        var line = content.Rows(row + 1, 1);
        var chip = Skin.Inlaid;

        line.Fill(chip.Text);

        var at = 1;

        if (operation.Host.Length > 0)
        {
            line.Write(0, at, operation.Host, chip.Remote);
            at += operation.Host.Length + 1;
        }

        Value(line, operation, chip, fill, at);

        if (operation.FieldHint.Length > 0)
        {
            content.WriteLine(
                row,
                TextWidth.Truncate(operation.FieldHint, content.Width - label.Length - 2),
                coat.Label,
                Align.Right);
        }

        return row + 3;
    }

    private static void Value(SurfaceRegion line, Operation operation, Skin.Coat chip, Rgb fill, int at)
    {
        var room = Math.Max(1, line.Width - at - 2);
        var (text, caret, selection) = Shown(operation);

        if (operation.ChosenIndex >= 0)
        {
            line.Write(0, at, TextWidth.Truncate(text, room), chip.Text);

            return;
        }

        EntryRow.Draw(line, 0, at, room, text, caret, selection, Skin.Entry(chip.Text, fill));
    }

    /// <summary>
    /// What the field is written as, with the caret and the selection counted the same way. A secret shows
    /// one dot per symbol, so both are counted in symbols there rather than in characters.
    /// </summary>
    /// <param name="operation">What is being asked.</param>
    /// <returns>The text to write, where the caret is in it, and where the selection is.</returns>
    private static (string Text, int Caret, (int Start, int End) Selection) Shown(Operation operation)
    {
        var text = operation.Value;
        var caret = TextWidth.SnapToCluster(text, operation.Field.Caret);
        var (start, end) = TextEditing.Selection(operation.Field);

        return operation.Secret
            ? (new string('•', TextWidth.CountClusters(text)),
                TextWidth.CountClusters(text[..caret]),
                (TextWidth.CountClusters(text[..start]), TextWidth.CountClusters(text[..end])))
            : (text, caret, (start, end));
    }
}
