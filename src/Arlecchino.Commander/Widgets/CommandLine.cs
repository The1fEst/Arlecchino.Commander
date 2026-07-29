using System;
using System.Collections.Generic;
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
    private readonly List<string> _history;

    private string _text = "";
    private int _cursor;
    private int _place;

    public CommandLine(List<string> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        _history = history;
        _place = history.Count;
    }

    public bool IsEmpty => _text.Length == 0;

    /// <summary>Offers a key to the line.</summary>
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

        return key.Modifiers.HasFlag(ConsoleModifiers.Control) ? Held(key.Key) : Alone(key);
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

    public void Draw(SurfaceRegion region, string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        region.Write(0, 0, prompt, Theme.Accent);

        var room = region.Width - prompt.Length;

        if (room <= 1)
        {
            return;
        }

        var offset = Math.Max(0, _cursor - room + 1);
        var shown = _text[offset..Math.Min(_text.Length, offset + room)];

        region.Write(0, prompt.Length, shown, Theme.Default);
        region.Write(
            0,
            prompt.Length + (_cursor - offset),
            _cursor < _text.Length ? _text[_cursor].ToString() : " ",
            Theme.Selected);
    }

    private bool Held(ConsoleKey key)
    {
        if (IsEmpty)
        {
            return false;
        }

        switch (key)
        {
            case ConsoleKey.A:
                _cursor = 0;
                return true;
            case ConsoleKey.E:
                _cursor = _text.Length;
                return true;
            case ConsoleKey.K:
                _text = _text[.._cursor];
                return true;
            case ConsoleKey.W:
                Word();
                return true;
            default:
                return false;
        }
    }

    private bool Alone(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Backspace when _cursor > 0:
                _text = _text.Remove(_cursor - 1, 1);
                _cursor--;
                return true;
            case ConsoleKey.Delete when _cursor < _text.Length:
                _text = _text.Remove(_cursor, 1);
                return true;
            case ConsoleKey.LeftArrow when _cursor > 0:
                _cursor--;
                return true;
            case ConsoleKey.RightArrow when _cursor < _text.Length:
                _cursor++;
                return true;
            case ConsoleKey.Home when !IsEmpty:
                _cursor = 0;
                return true;
            case ConsoleKey.End when !IsEmpty:
                _cursor = _text.Length;
                return true;
            case ConsoleKey.Escape when !IsEmpty:
                Clear();
                return true;
            case ConsoleKey.Spacebar when !IsEmpty:
                return Typed(' ');
            default:
                return Typed(key.KeyChar);
        }
    }

    /// <summary>
    /// One typed character. An empty line leaves the panel its own keys: Space marks a file and
    /// <c>+</c>, <c>-</c> and <c>*</c> work the marks, so none of them start a command.
    /// </summary>
    /// <param name="typed">The character.</param>
    /// <returns><c>true</c> when it went into the line.</returns>
    private bool Typed(char typed)
    {
        if (typed == '\0' || char.IsControl(typed))
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
