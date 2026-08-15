using System;
using Arlecchino.Commander.Model;
using Arlecchino.Editing;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// What is written on the command line and where the caret sits in it. The editing is the framework's own,
/// so the line behaves as a dialog field does, down to what counts as one symbol.
/// </summary>
internal sealed class CommandLineText : ITextEntry
{
    private string _text = "";
    private int _caret;

    /// <summary>
    /// What is on the line. Putting a whole line there leaves the caret at the end of it, as recalling a
    /// command does.
    /// </summary>
    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            _caret = value.Length;
        }
    }

    /// <summary>Where the caret is, counted from the start in characters and never past the end.</summary>
    public int Caret
    {
        get => _caret;
        set => _caret = Math.Clamp(value, 0, _text.Length);
    }

    /// <summary>
    /// Whether there is anything to run. A line holding nothing but spaces counts as empty: it looks
    /// empty, and running it would start a shell for nothing.
    /// </summary>
    public bool IsEmpty => Text.Trim().Length == 0;

    /// <summary>Puts a piece in where the caret is and leaves the caret after it.</summary>
    /// <param name="piece">What to put in.</param>
    public void Insert(string piece)
    {
        foreach (var character in piece)
        {
            TextEditing.Insert(this, character);
        }
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

        TextEditing.Insert(this, typed);

        return true;
    }

    /// <summary>Puts pasted text in, as much of it as one row can hold.</summary>
    /// <param name="text">What was pasted.</param>
    public void Paste(string text) => Insert(Pasted.OneLine(text));

    /// <summary>Rubs out the symbol before the caret.</summary>
    public void Back() => TextEditing.Backspace(this);

    /// <summary>Rubs out the symbol the caret is on, leaving the caret where it is.</summary>
    public void Ahead() => TextEditing.Delete(this);

    /// <summary>Rubs out the word before the caret, together with the spaces standing between them.</summary>
    public void Word() => TextEditing.EraseWord(this);

    /// <summary>Rubs out everything before the caret.</summary>
    public void EraseToStart() => TextEditing.EraseToStart(this);

    /// <summary>Moves the caret one symbol towards the start.</summary>
    public void Left() => TextEditing.MoveCaret(this, -1);

    /// <summary>Moves the caret one symbol towards the end.</summary>
    public void Right() => TextEditing.MoveCaret(this, 1);

    /// <summary>Moves the caret to the start of the word before it, over the spaces in between.</summary>
    public void WordLeft() => TextEditing.MoveWord(this, -1);

    /// <summary>Moves the caret past the end of the word after it, over the spaces in between.</summary>
    public void WordRight() => TextEditing.MoveWord(this, 1);

    /// <summary>Moves the caret to the start.</summary>
    public void First() => TextEditing.MoveToStart(this);

    /// <summary>Moves the caret to the end.</summary>
    public void Last() => TextEditing.MoveToEnd(this);
}
