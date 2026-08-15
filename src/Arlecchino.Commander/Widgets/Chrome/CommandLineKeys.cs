using System;
using Arlecchino.Hosting;
using Arlecchino.Input;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// Which key does what to the text on the line. Every key it recognizes is matched against the
/// application's own bindings rather than against a <see cref="ConsoleKey"/>, because a terminal
/// that reports no virtual key still sends the character.
/// </summary>
internal sealed class CommandLineKeys
{
    private readonly ArlecchinoKeymap _keymap;
    private readonly KeyText _keys;
    private readonly char _opens;

    /// <summary>Reads keys by one keymap and one layout.</summary>
    /// <param name="keymap">The keys the application obeys.</param>
    /// <param name="keys">
    /// Turns a key press into the character it types. Asking this rather than reading the key's own
    /// character is what lets a command be typed with a Cyrillic layout left switched on.
    /// </param>
    /// <param name="opens">The character that asks for the line.</param>
    public CommandLineKeys(ArlecchinoKeymap keymap, KeyText keys, char opens)
    {
        _keymap = keymap;
        _keys = keys;
        _opens = opens;
    }

    /// <summary>
    /// Whether a key press is the one that asks for the line, read as the character it types rather than as
    /// a key. Shift is forgiven, since the characters this is asked about are typed with it held.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when the line should be opened.</returns>
    public bool Opens(KeyPress key) =>
        (key.Modifiers & ~KeyModifiers.Shift) == KeyModifiers.None && _keys.Resolve(key) == _opens;

    /// <summary>Whether a key press is the one that recalls a command, and which way it goes.</summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns>Backwards, forwards, or nothing when the key is neither.</returns>
    public static bool? Recalls(KeyPress key) => key.Modifiers != KeyModifiers.Control
        ? null
        : key.Key switch
        {
            ConsoleKey.P => true,
            ConsoleKey.Y => false,
            _ => null,
        };

    /// <summary>Whether a key press is the one that gives the keyboard back.</summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when the line should be closed.</returns>
    public bool Closes(KeyPress key) => _keymap.Cancel.Matches(key);

    /// <summary>Rubbing out, moving about and typing, for a key the line has been offered.</summary>
    /// <param name="key">The key that arrived.</param>
    /// <param name="text">The text to work on.</param>
    /// <returns><c>true</c> when the key was one of these and has been dealt with.</returns>
    public bool Handle(KeyPress key, CommandLineText text) => Edits(key, text) || Typed(key, text);

    /// <summary>
    /// Rubbing out and moving about: every key that changes the line without typing a character. These are
    /// asked before the modified keys the application claims, since a word is stepped over on one of them.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <param name="text">The text to work on.</param>
    /// <returns><c>true</c> when the key was one of these and has been dealt with.</returns>
    public bool Edits(KeyPress key, CommandLineText text) => Erasing(key, text) || Moving(key, text);

    private bool Erasing(KeyPress key, CommandLineText text)
    {
        if (_keymap.Erase.Matches(key))
        {
            text.Back();
            return true;
        }

        if (_keymap.EraseWord.Matches(key))
        {
            text.Word();
            return true;
        }

        if (_keymap.EraseToStart.Matches(key))
        {
            text.EraseToStart();
            return true;
        }

        if (!_keymap.DeleteForward.Matches(key))
        {
            return false;
        }

        text.Ahead();

        return true;
    }

    private bool Moving(KeyPress key, CommandLineText text)
    {
        if (_keymap.WordLeft.Matches(key))
        {
            text.WordLeft();
            return true;
        }

        if (_keymap.WordRight.Matches(key))
        {
            text.WordRight();
            return true;
        }

        if (_keymap.MoveLeft.Matches(key))
        {
            text.Left();
            return true;
        }

        if (_keymap.MoveRight.Matches(key))
        {
            text.Right();
            return true;
        }

        if (_keymap.First.Matches(key))
        {
            text.First();
            return true;
        }

        if (!_keymap.Last.Matches(key))
        {
            return false;
        }

        text.Last();

        return true;
    }

    private bool Typed(KeyPress key, CommandLineText text) => _keys.Resolve(key) is { } typed && text.Put(typed);
}
