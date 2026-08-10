using System;
using System.Collections.Generic;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Rendering;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// The line under the panels. It is asked for rather than fallen into: a character opens it, Escape closes
/// it, and until then every letter belongs to the panel.
///
/// Which character that is comes from whoever built the line, because there is more than one line: the colon
/// opens the one commands are typed on, and the exclamation mark opens the one settings are typed on. They
/// are the same row and the same editing, and what tells them apart is that character and the words at
/// either end of the row.
///
/// Typing used to land here straight away, the way it does in Midnight Commander, and that spent the whole
/// alphabet on one thing. Every key the panel wanted then had to be held with a modifier, and a modifier is
/// what a window manager, a terminal and the ASCII control codes each take a bite out of first. A key that
/// asks for the line back is the cheaper end of that trade.
///
/// What is written on the line is <see cref="CommandLineText"/>, which key does what to it is <see cref="CommandLineKeys"/>,
/// and the row it all ends up on is <see cref="CommandLinePaint"/>. What is left here is who has the keyboard and
/// which command was typed before this one.
/// </summary>
public sealed class CommandLine
{
    private readonly List<string> _history;
    private readonly CommandLineKeys _keys;
    private readonly CommandLineText _text = new();

    private int _place;

    /// <summary>Creates the line.</summary>
    /// <param name="history">Commands run before, shared with whatever else remembers them.</param>
    /// <param name="keys">
    /// Turns a key press into the character it types. Asking this rather than reading the key's own
    /// character is what lets a command be typed with a Cyrillic layout left switched on.
    /// </param>
    /// <param name="keymap">The keys the application obeys, which the line edits by.</param>
    /// <param name="opens">The character that asks for the line.</param>
    public CommandLine(List<string> history, KeyText keys, ArlecchinoKeymap keymap, char opens)
    {
        ArgumentNullException.ThrowIfNull(history);

        _history = history;
        _keys = new(keymap, keys, opens);
        _place = history.Count;
    }

    /// <summary>
    /// Whether there is a command being typed. A line holding nothing but spaces counts as empty: it
    /// looks empty, and running it would start a shell for nothing.
    /// </summary>
    public bool IsEmpty => _text.IsEmpty;

    /// <summary>Whether the line has the keyboard, which is what tells a typed letter from a pressed key.</summary>
    public bool IsTyping { get; private set; }

    /// <summary>
    /// What is on the line as it stands, for whoever has to answer while it is still being typed — hints
    /// that narrow with every letter, and the completion behind Tab.
    /// </summary>
    public string Typed => _text.Text;

    /// <summary>
    /// Whether a key press is the one that asks for the line. It is read as the character it types rather
    /// than as a key, so a layout that puts the colon somewhere else still opens it.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when the line should be opened.</returns>
    public bool Opens(KeyPress key) => !IsTyping && _keys.Opens(key);

    /// <summary>Hands the line the keyboard.</summary>
    public void Open() => IsTyping = true;

    /// <summary>Gives the keyboard back to the panel, and forgets what was half typed.</summary>
    public void Close()
    {
        IsTyping = false;

        _text.Clear();
        _place = _history.Count;
    }

    /// <summary>
    /// Offers a key to the line, which takes everything while it has the keyboard.
    ///
    /// Escape gives the keyboard back rather than only wiping what is on the line — the line was asked
    /// for, so there is something to leave.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when the line took it and the panel should not see it.</returns>
    public bool Handle(KeyPress key)
    {
        if (!IsTyping)
        {
            return false;
        }

        if (CommandLineKeys.Recalls(key) is { } back)
        {
            return Recall(back);
        }

        if (key.Modifiers == KeyModifiers.Control)
        {
            return false;
        }

        if (!_keys.Closes(key))
        {
            return _keys.Handle(key, _text);
        }

        Close();

        return true;
    }

    /// <summary>Takes what is typed, remembering it, and leaves the line empty.</summary>
    /// <returns>The command.</returns>
    public string Take()
    {
        var command = _text.Text.Trim();

        Close();

        return command;
    }

    /// <summary>
    /// Puts a name or a path where the cursor is, with a space after it. The line takes the keyboard on
    /// the way: something was just written on it, and leaving the next letter to the panel would be a
    /// surprise nobody asked for.
    /// </summary>
    /// <param name="piece">What to put in.</param>
    public void Insert(string piece)
    {
        ArgumentNullException.ThrowIfNull(piece);

        var quoted = piece.Contains(' ', StringComparison.Ordinal) ? $"\"{piece}\"" : piece;

        _text.Insert(quoted + " ");

        Open();
    }

    /// <summary>
    /// Puts pasted text where the cursor is, taking the keyboard on the way as <see cref="Insert"/> does.
    /// A terminal delivers a paste as a block of its own rather than as the keys that would have typed it.
    /// A line that reads keys alone never sees one, and text that goes nowhere is what reads as a paste
    /// that does not work.
    ///
    /// Only the first line of it lands here, since this is one row and one command.
    /// </summary>
    /// <param name="text">What was pasted, with the terminal's markers already stripped.</param>
    public void Paste(string text)
    {
        _text.Paste(text);

        Open();
    }

    /// <summary>
    /// Puts a whole line there, with the caret after it. What completing a half-typed word comes to, and
    /// what filling in the value a setting already has comes to.
    /// </summary>
    /// <param name="text">What the line should say.</param>
    public void Set(string text)
    {
        _text.Set(text);

        Open();
    }

    /// <summary>Draws the line, prompted with where what is typed would land.</summary>
    /// <param name="region">The row to draw on.</param>
    /// <param name="prompt">Where it would land.</param>
    /// <param name="tail">What the far end of the row says.</param>
    public void Draw(SurfaceRegion region, string prompt, string tail)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        CommandLinePaint.Draw(region, _text, IsTyping, prompt, tail);
    }

    /// <summary>
    /// Puts an earlier command on the line, one step at a time. Walking past the last of them leaves the
    /// line empty rather than stuck on it, which is where a command of one's own gets typed.
    /// </summary>
    /// <param name="back"><c>true</c> to go back through them, <c>false</c> to come forward.</param>
    /// <returns><c>true</c> when there was a history to walk.</returns>
    private bool Recall(bool back)
    {
        if (_history.Count == 0)
        {
            return false;
        }

        _place = back ? Math.Max(0, _place - 1) : Math.Min(_history.Count, _place + 1);

        _text.Set(_place == _history.Count ? "" : _history[_place]);

        return true;
    }
}
