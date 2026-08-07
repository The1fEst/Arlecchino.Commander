using System;
using System.Collections.Generic;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;
using Arlecchino.Rendering.Colors;
using Arlecchino.Commander.Widgets.Chrome;

namespace Arlecchino.Commander.Widgets.Dialogs;

/// <summary>How loud an operation is, which decides the color of everything it is asked about in.</summary>
public enum Weight
{
    /// <summary>It moves data about: copying, moving.</summary>
    Moves,

    /// <summary>It destroys data. Only deleting.</summary>
    Destroys,

    /// <summary>It is reversible and local: renaming, making a folder, changing a mode.</summary>
    Reversible,
}

/// <summary>What an operation will actually do, in plain words.</summary>
/// <param name="Text">The words.</param>
/// <param name="Warns">Whether it is a warning rather than a remark.</param>
public sealed record Remark(string Text, bool Warns = false);

/// <summary>One switch on an operation, and whether it is on.</summary>
/// <param name="Label">What it says.</param>
/// <param name="On">Whether it is ticked.</param>
public sealed record Choice(string Label, bool On)
{
    /// <summary>Whether it is ticked, which is the one thing about a choice that changes.</summary>
    public bool On { get; set; } = On;
}

/// <summary>
/// Everything the application ever asks before doing something to a file, in one shape.
///
/// Copying, moving, deleting, renaming, making a folder and changing a mode differ in what they act
/// on and what they need told; they do not differ in what the question looks like. One shell for all
/// six means a seventh is a row in a table rather than a dialog somebody has to design — and it means
/// none of them can quietly forget to say, in words, what is about to happen.
/// </summary>
public sealed class Operation
{
    /// <summary>What the dialog is called.</summary>
    public required string Title { get; init; }

    /// <summary>
    /// The line under the title, for what the title cannot say by itself — which folder is being
    /// deleted from, which file is being renamed. Left out when it would only restate the title.
    /// </summary>
    public string Subtitle { get; init; } = "";

    /// <summary>The key that opened it, written as a badge in the corner.</summary>
    public required string Key { get; init; }

    /// <summary>The word on the button that goes through with it.</summary>
    public required string Verb { get; init; }

    /// <summary>How loud it is.</summary>
    public required Weight Weight { get; init; }

    /// <summary>
    /// What will actually happen, for the operations where the verb does not say it. It is worked out from
    /// what has been typed rather than fixed when the dialog opened, so a name that turns out to be taken
    /// says so while it is being typed. Nothing where the verb is answer enough: a band that restates the
    /// button teaches people to stop reading the band.
    /// </summary>
    public Func<Operation, Remark?>? Note { get; init; }

    /// <summary>What it acts on. Empty for the operations that act on one thing named in the subtitle.</summary>
    public IReadOnlyList<FileEntry> Items { get; init; } = [];

    /// <summary>What to call the list of them.</summary>
    public string ItemsLabel { get; init; } = Loc(LocString.OperationWhat);

    /// <summary>What the one field is for, or nothing when there is no field — as for deleting.</summary>
    public string? FieldLabel { get; init; }

    /// <summary>What stands in front of the field: a host, or the letters of a mode.</summary>
    public string Host { get; init; } = "";

    /// <summary>The hint at the right of the field.</summary>
    public string FieldHint { get; init; } = "";

    /// <summary>Whether the field holds a secret, and so is drawn as dots rather than as itself.</summary>
    public bool Secret { get; init; }

    /// <summary>What is typed in the field.</summary>
    public string Value { get; set; } = "";

    /// <summary>Where the caret is in it.</summary>
    public int Caret { get; set; }

    /// <summary>The switches, in the order they are drawn.</summary>
    public IReadOnlyList<Choice> Options { get; init; } = [];

    /// <summary>
    /// Where the field's answer lives, when the answer is a path. Tab completes against it: typing a
    /// destination in full is the slowest thing this dialog ever asks anybody to do.
    /// </summary>
    public IFileSource? Over { get; init; }

    /// <summary>What to do when it is gone through with.</summary>
    public required Action<Operation> Confirm { get; init; }

    /// <summary>Which switch the cursor is on, or <c>-1</c> when it is in the field.</summary>
    public int Chosen { get; set; } = -1;

    /// <summary>Whether a switch is on, by the words on it.</summary>
    /// <param name="label">Which switch.</param>
    /// <returns><c>true</c> when it is ticked.</returns>
    public bool Ticked(string label)
    {
        foreach (var option in Options)
        {
            if (option.Label == label)
            {
                return option.On;
            }
        }

        return false;
    }

    /// <summary>The colors this operation is asked about in.</summary>
    /// <returns>The fill, the text on it, and the band a note sits on.</returns>
    public (Rgb Fill, Rgb Text, Rgb Band) Tone() => Weight switch
    {
        Weight.Destroys => (Skin.Danger, new(0xFF, 0xF1, 0xEF), Skin.Blend(Skin.Danger, 0.16, Skin.Over)),
        Weight.Reversible => (Skin.Calm, new(0xF0, 0xF7, 0xF3), Skin.Blend(Skin.Calm, 0.15, Skin.Over)),
        _ => (Skin.Crimson, Skin.OnCrimson, Skin.Blend(Skin.Crimson, 0.13, Skin.Over)),
    };

    /// <summary>Moves the cursor between the field and the switches.</summary>
    /// <param name="forward">Which way.</param>
    public void Step(bool forward)
    {
        var stops = Options.Count + (FieldLabel is null ? 0 : 1);

        if (stops == 0)
        {
            return;
        }

        var first = FieldLabel is null ? 0 : -1;
        var at = Chosen - first + (forward ? 1 : -1);

        Chosen = ((at % stops) + stops) % stops + first;
    }

    /// <summary>Turns the switch under the cursor over.</summary>
    /// <returns><c>true</c> when there was one to turn.</returns>
    public bool Toggle()
    {
        if (Chosen < 0 || Chosen >= Options.Count)
        {
            return false;
        }

        Options[Chosen].On = !Options[Chosen].On;

        return true;
    }

    /// <summary>Puts a character into the field.</summary>
    /// <param name="typed">What was typed.</param>
    public void Put(char typed)
    {
        Caret = Math.Clamp(Caret, 0, Value.Length);
        Value = Value.Insert(Caret, typed.ToString());
        Caret++;
    }

    /// <summary>Rubs out the character before the caret.</summary>
    public void Back()
    {
        Caret = Math.Clamp(Caret, 0, Value.Length);

        if (Caret == 0)
        {
            return;
        }

        Value = Value.Remove(Caret - 1, 1);
        Caret--;
    }

    /// <summary>Moves the caret along the field.</summary>
    /// <param name="by">How far, and which way.</param>
    public void Nudge(int by) => Caret = Math.Clamp(Caret + by, 0, Value.Length);
}
