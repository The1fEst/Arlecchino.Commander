using System;
using Arlecchino.Editing;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// What is written on the command line, where the caret sits in it and what is selected. The editing is the
/// framework's own, so the line behaves as a dialog field does, down to what counts as one symbol.
/// </summary>
internal sealed class CommandLineText : ITextEntry
{
    private string _text = "";
    private int _caret;
    private int _anchor;

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
            _anchor = value.Length;
        }
    }

    /// <summary>Where the caret is, counted from the start in characters and never past the end.</summary>
    public int Caret
    {
        get => _caret;
        set => _caret = Math.Clamp(value, 0, _text.Length);
    }

    /// <summary>Where the selection was started from, on the caret while nothing is selected.</summary>
    public int Anchor
    {
        get => _anchor;
        set => _anchor = Math.Clamp(value, 0, _text.Length);
    }

    /// <summary>
    /// Whether there is anything to run. A line holding nothing but spaces counts as empty: it looks
    /// empty, and running it would start a shell for nothing.
    /// </summary>
    public bool IsEmpty => Text.Trim().Length == 0;

    /// <summary>Puts a piece in where the caret is, over whatever was selected.</summary>
    /// <param name="piece">What to put in.</param>
    public void Insert(string piece) => TextEditing.InsertText(this, piece);

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
    public void Paste(string text) =>
        Insert(PastedText.FirstLine(text, static character => !char.IsControl(character)));
}
