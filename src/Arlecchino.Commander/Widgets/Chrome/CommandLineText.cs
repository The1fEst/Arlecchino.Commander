using System;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// What is written on the command line and where the caret sits in it. Every way a character gets in or
/// out is here, so nothing that reads a key has to know how a string is cut about.
/// </summary>
internal sealed class CommandLineText
{
    /// <summary>What is on the line.</summary>
    public string Text { get; private set; } = "";

    /// <summary>Where the caret is, counted from the start in characters.</summary>
    public int Cursor { get; private set; }

    /// <summary>
    /// Whether there is anything to run. A line holding nothing but spaces counts as empty: it looks
    /// empty, and running it would start a shell for nothing.
    /// </summary>
    public bool IsEmpty => Text.Trim().Length == 0;

    /// <summary>Leaves the line empty, with the caret back at the start.</summary>
    public void Clear()
    {
        Text = "";
        Cursor = 0;
    }

    /// <summary>Puts a whole line there and leaves the caret at the end of it, as recalling one does.</summary>
    /// <param name="text">What the line should say.</param>
    public void Set(string text)
    {
        Text = text;
        Cursor = text.Length;
    }

    /// <summary>Puts a piece in where the caret is and leaves the caret after it.</summary>
    /// <param name="piece">What to put in.</param>
    public void Insert(string piece)
    {
        if (piece.Length == 0)
        {
            return;
        }

        Text = Text.Insert(Cursor, piece);
        Cursor += piece.Length;
    }

    /// <summary>
    /// One typed character. The control codes are refused, since a terminal sends them for keys that mean
    /// something else entirely.
    /// </summary>
    /// <param name="typed">The character the key types.</param>
    /// <returns><c>true</c> when it went in.</returns>
    public bool Put(char typed)
    {
        if (char.IsControl(typed))
        {
            return false;
        }

        Insert(typed.ToString());

        return true;
    }

    /// <summary>Puts pasted text in, as much of it as one row can hold.</summary>
    /// <param name="text">What was pasted.</param>
    public void Paste(string text) => Insert(Pasted.OneLine(text));

    /// <summary>Rubs out the character before the caret.</summary>
    public void Back()
    {
        if (Cursor == 0)
        {
            return;
        }

        Text = Text.Remove(Cursor - 1, 1);
        Cursor--;
    }

    /// <summary>Rubs out the character the caret is on, leaving the caret where it is.</summary>
    public void Ahead()
    {
        if (Cursor < Text.Length)
        {
            Text = Text.Remove(Cursor, 1);
        }
    }

    /// <summary>Rubs out the word before the caret, back to the space that starts it.</summary>
    public void Word()
    {
        var cut = Text.LastIndexOf(' ', Math.Max(0, Cursor - 1));

        Text = Text.Remove(cut + 1, Cursor - cut - 1);
        Cursor = cut + 1;
    }

    /// <summary>Rubs out everything before the caret.</summary>
    public void EraseToStart()
    {
        Text = Text[Cursor..];
        Cursor = 0;
    }

    /// <summary>Moves the caret one character towards the start.</summary>
    public void Left() => Cursor = Math.Max(0, Cursor - 1);

    /// <summary>Moves the caret one character towards the end.</summary>
    public void Right() => Cursor = Math.Min(Text.Length, Cursor + 1);

    /// <summary>Moves the caret to the start.</summary>
    public void First() => Cursor = 0;

    /// <summary>Moves the caret to the end.</summary>
    public void Last() => Cursor = Text.Length;
}
