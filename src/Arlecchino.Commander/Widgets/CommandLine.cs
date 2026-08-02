using System;
using System.Collections.Generic;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Rendering;

namespace Arlecchino.Commander.Widgets;

/// <summary>
/// The line under the panels. It never takes the focus: the panel keeps it and typing lands here, the
/// way it does in Midnight Commander, which is why every key it claims is one the panel has no use
/// for while there is something typed. An empty line claims almost nothing, so Space still marks and
/// Backspace still leaves the folder.
/// </summary>
public sealed class CommandLine
{
    private const int SideRoom = 2;
    private static string Tail => Loc(LocString.CommandLineTail);

    private readonly List<string> _history;
    private readonly KeyText _keys;
    private readonly ArlecchinoKeymap _keymap;

    private string _text = "";
    private int _cursor;
    private int _place;

    /// <summary>Creates the line.</summary>
    /// <param name="history">Commands run before, shared with whatever else remembers them.</param>
    /// <param name="keys">
    /// Turns a key press into the character it types. Asking this rather than reading the key's own
    /// character is what lets a command be typed with a Cyrillic layout left switched on.
    /// </param>
    /// <param name="keymap">The keys the application obeys, which the line edits by.</param>
    public CommandLine(List<string> history, KeyText keys, ArlecchinoKeymap keymap)
    {
        ArgumentNullException.ThrowIfNull(history);

        _history = history;
        _keys = keys;
        _keymap = keymap;
        _place = history.Count;
    }

    /// <summary>
    /// Whether there is a command being typed. A line holding nothing but spaces counts as empty: it
    /// looks empty, and a stray space left on it would otherwise quietly take every Space after it away
    /// from the panel, which reads as marking having stopped working.
    /// </summary>
    public bool IsEmpty => _text.Trim().Length == 0;

    /// <summary>
    /// Offers a key to the line. Every key it recognises is matched against the application's own
    /// bindings rather than against a <see cref="ConsoleKey"/>, because a terminal that reports no
    /// virtual key still sends the character — and a Backspace the line failed to recognise is a
    /// Backspace the panel takes, which walks out of the folder mid-command.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when the line took it and the panel should not see it.</returns>
    public bool Handle(ConsoleKeyInfo key)
    {
        if (key.Modifiers.HasFlag(ConsoleModifiers.Alt))
        {
            return key.Key switch
            {
                ConsoleKey.P => Recall(back: true),
                ConsoleKey.N => Recall(back: false),
                _ => false,
            };
        }

        return IsEmpty ? Typed(key) : Editing(key);
    }

    /// <summary>
    /// A key offered to a line that already has something on it, where the line claims everything it
    /// knows what to do with.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when the line took it.</returns>
    private bool Editing(ConsoleKeyInfo key)
    {
        if (_keymap.Erase.Matches(key))
        {
            Back();
            return true;
        }

        if (_keymap.EraseWord.Matches(key))
        {
            Word();
            return true;
        }

        if (_keymap.EraseToStart.Matches(key))
        {
            _text = _text[_cursor..];
            _cursor = 0;

            return true;
        }

        if (_keymap.DeleteForward.Matches(key))
        {
            Ahead();
            return true;
        }

        if (!_keymap.Cancel.Matches(key))
        {
            return Moving(key) || Typed(key);
        }

        Clear();

        return true;
    }

    private void Back()
    {
        if (_cursor == 0)
        {
            return;
        }

        _text = _text.Remove(_cursor - 1, 1);
        _cursor--;
    }

    private void Ahead()
    {
        if (_cursor < _text.Length)
        {
            _text = _text.Remove(_cursor, 1);
        }
    }

    private bool Moving(ConsoleKeyInfo key)
    {
        if (_keymap.MoveLeft.Matches(key))
        {
            _cursor = Math.Max(0, _cursor - 1);
            return true;
        }

        if (_keymap.MoveRight.Matches(key))
        {
            _cursor = Math.Min(_text.Length, _cursor + 1);
            return true;
        }

        if (_keymap.First.Matches(key))
        {
            _cursor = 0;
            return true;
        }

        if (!_keymap.Last.Matches(key))
        {
            return false;
        }

        _cursor = _text.Length;

        return true;
    }

    /// <summary>Takes what is typed, remembering it, and leaves the line empty.</summary>
    /// <returns>The command.</returns>
    public string Take()
    {
        var command = _text.Trim();

        Clear();

        return command;
    }

    /// <summary>Puts a name or a path where the cursor is, with a space after it.</summary>
    /// <param name="piece">What to put in.</param>
    public void Insert(string piece)
    {
        ArgumentNullException.ThrowIfNull(piece);

        var quoted = piece.Contains(' ', StringComparison.Ordinal) ? $"\"{piece}\"" : piece;

        _text = _text.Insert(_cursor, quoted + " ");
        _cursor += quoted.Length + 1;
    }

    private void Clear()
    {
        _text = "";
        _cursor = 0;
        _place = _history.Count;
    }

    /// <summary>
    /// Where the command would run, then the prompt, then what has been typed. The caret is a block in
    /// the accent rather than the terminal's own: the line is never the focused widget, so the terminal
    /// would put its cursor somewhere else entirely.
    /// </summary>
    /// <param name="region">The row to draw on.</param>
    /// <param name="prompt">Where the command would run.</param>
    public void Draw(SurfaceRegion region, string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var coat = Skin.Quiet;

        region.Fill(coat.Text);
        region.Write(0, SideRoom, prompt, coat.Faded);

        var mark = SideRoom + prompt.Length + 1;

        region.Write(0, mark, "❯", Skin.Paint(Skin.Crimson, Skin.Unlit, TextStyle.Bold));

        var at = mark + 2;
        var room = region.Width - at - Tail.Length - SideRoom - 2;

        if (room <= 1)
        {
            return;
        }

        var offset = Math.Max(0, _cursor - room + 1);
        var shown = _text[offset..Math.Min(_text.Length, offset + room)];

        region.Write(0, at, shown, coat.Text);
        region.Write(
            0,
            at + (_cursor - offset),
            _cursor < _text.Length ? _text[_cursor].ToString() : " ",
            Skin.Paint(Skin.Ink, Skin.Crimson));

        if (region.Width > at + room + Tail.Length)
        {
            region.Write(0, region.Width - Tail.Length - SideRoom, Tail, coat.Ghost);
        }
    }

    /// <summary>
    /// One typed character. An empty line leaves the panel its own keys: Space marks a file and
    /// <c>+</c>, <c>-</c> and <c>*</c> work the marks, so none of them start a command.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when it went into the line.</returns>
    private bool Typed(ConsoleKeyInfo key) => _keys.Resolve(key) is { } typed && Put(typed);

    private bool Put(char typed)
    {
        if (char.IsControl(typed))
        {
            return false;
        }

        if (IsEmpty && typed is ' ' or '+' or '-' or '*')
        {
            return false;
        }

        _text = _text.Insert(_cursor, typed.ToString());
        _cursor++;

        return true;
    }

    private void Word()
    {
        var cut = _text.LastIndexOf(' ', Math.Max(0, _cursor - 1));

        _text = _text.Remove(cut + 1, _cursor - cut - 1);
        _cursor = cut + 1;
    }

    private bool Recall(bool back)
    {
        if (_history.Count == 0)
        {
            return false;
        }

        _place = back ? Math.Max(0, _place - 1) : Math.Min(_history.Count, _place + 1);
        _text = _place == _history.Count ? "" : _history[_place];
        _cursor = _text.Length;

        return true;
    }
}
