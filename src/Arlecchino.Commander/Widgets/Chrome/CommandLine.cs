using System;
using System.Collections.Generic;
using Arlecchino.Commander.Model;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Colors;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// The line under the panels. It is asked for rather than fallen into: the colon opens it, Escape closes it,
/// and until then every letter belongs to the panel.
///
/// Typing used to land here straight away, the way it does in Midnight Commander, and that spent the whole
/// alphabet on one thing. Every key the panel wanted then had to be held with a modifier, and a modifier is
/// what a window manager, a terminal and the ASCII control codes each take a bite out of first. A key that
/// asks for the line back is the cheaper end of that trade.
/// </summary>
public sealed class CommandLine
{
    private const int SideRoom = 2;

    /// <summary>
    /// What the far end of the row says. A line nobody is typing on spends it on the key that wakes it,
    /// since a row that does nothing and explains nothing is a row that reads as broken.
    /// </summary>
    private string Tail => Loc(IsTyping ? LocString.CommandLineTail : LocString.CommandLineAsleep);

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
    /// looks empty, and running it would start a shell for nothing.
    /// </summary>
    public bool IsEmpty => _text.Trim().Length == 0;

    /// <summary>Whether the line has the keyboard, which is what tells a typed letter from a pressed key.</summary>
    public bool IsTyping { get; private set; }

    /// <summary>
    /// Whether a key press is the one that asks for the line. It is read as the character it types rather
    /// than as a key, so a layout that puts the colon somewhere else still opens it.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when the line should be opened.</returns>
    public bool Opens(KeyPress key) =>
        !IsTyping && key.Modifiers == default && _keys.Resolve(key) is ':';

    /// <summary>Hands the line the keyboard.</summary>
    public void Open() => IsTyping = true;

    /// <summary>Gives the keyboard back to the panel, and forgets what was half typed.</summary>
    public void Close()
    {
        IsTyping = false;

        Clear();
    }

    /// <summary>
    /// Offers a key to the line, which takes everything while it has the keyboard. Every key it recognizes
    /// is matched against the application's own bindings rather than against a <see cref="ConsoleKey"/>,
    /// because a terminal that reports no virtual key still sends the character.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when the line took it and the panel should not see it.</returns>
    public bool Handle(KeyPress key)
    {
        if (!IsTyping)
        {
            return false;
        }

        if (key.Modifiers == KeyModifiers.Control)
        {
            return key.Key switch
            {
                ConsoleKey.P => Recall(back: true),
                ConsoleKey.Y => Recall(back: false),
                _ => false,
            };
        }

        return Editing(key);
    }

    /// <summary>
    /// A key offered to the line while it has the keyboard, where it claims everything it knows what to do
    /// with. Escape gives the keyboard back rather than only wiping what is on the line — the line was
    /// asked for, so there is something to leave.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when the line took it.</returns>
    private bool Editing(KeyPress key)
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

        Close();

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

    private bool Moving(KeyPress key)
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

        _text = _text.Insert(_cursor, quoted + " ");
        _cursor += quoted.Length + 1;

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
        var piece = Pasted.OneLine(text);

        Open();

        if (piece.Length == 0)
        {
            return;
        }

        _text = _text.Insert(_cursor, piece);
        _cursor += piece.Length;
    }

    private void Clear()
    {
        _text = "";
        _cursor = 0;
        _place = _history.Count;
    }

    /// <summary>
    /// Where the command would run, then the prompt, then what has been typed.
    ///
    /// A line nobody is typing on says so. The chevron goes out, the path dims to a trace, and the caret
    /// is not drawn at all. A caret on a line that is not listening is the screen telling a lie, and it
    /// leaves the keyboard somewhere the eye cannot find it.
    ///
    /// The caret is a block in the accent rather than the terminal's own, since the line is not a focused
    /// widget and the terminal would put its cursor somewhere else entirely.
    /// </summary>
    /// <param name="region">The row to draw on.</param>
    /// <param name="prompt">Where the command would run.</param>
    public void Draw(SurfaceRegion region, string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var coat = Skin.Quiet;

        region.Fill(coat.Text);
        region.Write(0, SideRoom, prompt, IsTyping ? coat.Faded : coat.Sleeping);

        var mark = SideRoom + prompt.Length + 1;

        region.Write(
            0,
            mark,
            "❯",
            IsTyping ? Skin.Paint(Skin.Crimson, Skin.Unlit, TextStyle.Bold) : coat.Sleeping);

        var at = mark + 2;
        var room = region.Width - at - Tail.Length - SideRoom - 2;

        if (room <= 1)
        {
            return;
        }

        var offset = Math.Max(0, _cursor - room + 1);
        var shown = _text[offset..Math.Min(_text.Length, offset + room)];

        region.Write(0, at, shown, coat.Text);

        if (IsTyping)
        {
            region.Write(
                0,
                at + (_cursor - offset),
                _cursor < _text.Length ? _text[_cursor].ToString() : " ",
                Skin.Paint(Skin.Ink, Skin.Crimson));
        }

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
    private bool Typed(KeyPress key) => _keys.Resolve(key) is { } typed && Put(typed);

    private bool Put(char typed)
    {
        if (char.IsControl(typed))
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
