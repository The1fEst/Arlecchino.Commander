using System;
using System.Collections.Generic;
using Arlecchino.Atoms.Local;
using Arlecchino.Editing;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Rendering;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// The line under the panels, which is asked for by a character its owner names and closed by Escape. What
/// is kept here is who has the keyboard and which command was typed before this one.
/// </summary>
/// <seealso cref="CommandLineText"/>
/// <seealso cref="CommandLineKeys"/>
/// <seealso cref="CommandLinePaint"/>
public sealed class CommandLine
{
    private readonly List<string> _history;
    private readonly ArlecchinoKeymap _keymap;
    private readonly IArlecchinoTerminal _terminal;
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
    /// <param name="terminal">What reaches the clipboard, for the selection that is copied or cut.</param>
    /// <param name="opens">The character that asks for the line.</param>
    public CommandLine(
        List<string> history,
        KeyText keys,
        ArlecchinoKeymap keymap,
        IArlecchinoTerminal terminal,
        char opens)
    {
        _history = history;
        _keymap = keymap;
        _terminal = terminal;
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
    /// How many rows what is typed took, worked out while drawing rather than before. An atom, so the
    /// screen that leaves room for the line can lay itself out again on the frame after it changed.
    /// </summary>
    public LocalAtom<int> Height { get; } = new(1);

    /// <summary>
    /// What is on the line as it stands, for whoever has to answer while it is still being typed — hints
    /// that narrow with every letter, and the completion behind Tab.
    /// </summary>
    public string Typed => _text.Text;

    /// <summary>
    /// The text as something that can be edited from outside, for whatever is hung on a line being typed
    /// into. Completion is one such thing: it finishes the word under the caret.
    /// </summary>
    public ITextEntry Entry => _text;

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

        _text.Text = "";
        _place = _history.Count;
    }

    /// <summary>
    /// Offers a key to the line, which takes everything while it has the keyboard. Escape gives the
    /// keyboard back rather than only wiping what is on the line.
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

        if (Clipped(key) || _keys.Edits(key, _text))
        {
            return true;
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
    /// the way, so the next letter typed lands on it rather than on the panel.
    /// </summary>
    /// <param name="piece">What to put in.</param>
    public void Insert(string piece)
    {
        var quoted = piece.Contains(' ', StringComparison.Ordinal) ? $"\"{piece}\"" : piece;

        _text.Insert(quoted + " ");

        Open();
    }

    /// <summary>
    /// Puts pasted text where the cursor is, taking the keyboard on the way as <see cref="Insert"/> does.
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
        _text.Text = text;

        Open();
    }

    /// <summary>
    /// Draws the line, prompted with where what is typed would land. How many rows it took goes on
    /// <see cref="Height"/>, since a long command is carried onto rows the screen has to leave room for.
    /// </summary>
    /// <param name="region">The rows to draw on.</param>
    /// <param name="prompt">Where it would land.</param>
    /// <param name="tail">What the far end of the first row says.</param>
    public void Draw(SurfaceRegion region, string prompt, string tail)
    {
        Height.Value = CommandLinePaint.Draw(region, _text, IsTyping, prompt, tail);
    }

    /// <summary>
    /// Copying and cutting, which take the selection where there is one and the whole line where there is
    /// not. Cutting an empty line would leave nothing to run, so it takes the selection alone.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when the key was one of these and has been dealt with.</returns>
    private bool Clipped(KeyPress key)
    {
        var selected = TextEditing.Selected(_text);

        if (_keymap.Copy.Matches(key))
        {
            _terminal.CopyToClipboard(selected.Length > 0 ? selected : _text.Text);

            return true;
        }

        if (!_keymap.Cut.Matches(key) || selected.Length == 0)
        {
            return false;
        }

        _terminal.CopyToClipboard(selected);
        TextEditing.EraseSelection(_text);

        return true;
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

        _text.Text = _place == _history.Count ? "" : _history[_place];

        return true;
    }
}
