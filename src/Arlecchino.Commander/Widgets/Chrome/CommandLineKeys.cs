using System;
using Arlecchino.Editing;
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
    private readonly char _opens;

    /// <summary>Reads keys by one keymap and one layout.</summary>
    /// <param name="keymap">The keys the application obeys.</param>
    /// <param name="opens">The character that asks for the line.</param>
    public CommandLineKeys(ArlecchinoKeymap keymap, char opens)
    {
        _keymap = keymap;
        _opens = opens;
    }

    /// <summary>
    /// Whether a key press is the one that asks for the line, read as the character it types and as the
    /// character its key sits at. Shift is forgiven, since the characters this is asked about are typed
    /// with it held.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when the line should be opened.</returns>
    public bool Opens(KeyPress key) =>
        (key.Modifiers & ~KeyModifiers.Shift) == KeyModifiers.None && KeyText.Shortcut(key) == _opens;

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
    public bool Handle(KeyPress key, CommandLineText text) => Edits(key, text) || Text(key, text);

    /// <summary>
    /// Selecting, moving about and rubbing out: every key that changes the line without typing a character.
    /// These are asked before the modified keys the application claims, since a word is stepped over on one.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <param name="text">The text to work on.</param>
    /// <returns><c>true</c> when the key was one of these and has been dealt with.</returns>
    public bool Edits(KeyPress key, CommandLineText text) =>
        SelectKeys.Handled(text, _keymap, key) ||
        CaretKeys.Moved(text, _keymap, key) ||
        EraseKeys.Erased(text, _keymap, key);

    private static bool Text(KeyPress key, CommandLineText text) =>
        KeyText.Resolve(key) is { } character && text.Put(character);
}
