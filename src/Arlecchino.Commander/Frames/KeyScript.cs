using System;
using System.Globalization;
using Arlecchino.Input;

namespace Arlecchino.Commander.Frames;

public static class KeyScript
{
    public static KeyPress One(string piece)
    {
        var modifiers = KeyModifiers.None;
        var name = piece;

        while (Prefix(name) is { } split)
        {
            modifiers |= split.Modifier;
            name = split.Tail;
        }

        return Named(name) is { } key ? new(key, modifiers, Character(key)) : Typed(name, modifiers);
    }

    /// <summary>
    /// The character a terminal sends along with a named key. Space is the one that matters: a screen
    /// that reads what was typed sees a space, not a key with no character to it.
    /// </summary>
    /// <param name="key">The key the name stood for.</param>
    /// <returns>The character, or nothing for keys that carry none.</returns>
    private static char Character(ConsoleKey key) => key switch
    {
        ConsoleKey.Spacebar => ' ',
        ConsoleKey.Enter => '\r',
        ConsoleKey.Tab => '\t',
        ConsoleKey.Escape => '\e',
        ConsoleKey.Backspace => '\b',
        _ => (char)0,
    };

    private static (KeyModifiers Modifier, string Tail)? Prefix(string name)
    {
        if (name.StartsWith("Ctrl+", StringComparison.OrdinalIgnoreCase))
        {
            return (KeyModifiers.Control, name[5..]);
        }

        if (name.StartsWith("Alt+", StringComparison.OrdinalIgnoreCase))
        {
            return (KeyModifiers.Alt, name[4..]);
        }

        if (name.StartsWith("Cmd+", StringComparison.OrdinalIgnoreCase))
        {
            return (KeyModifiers.Super, name[4..]);
        }

        return name.StartsWith("Shift+", StringComparison.OrdinalIgnoreCase)
            ? (KeyModifiers.Shift, name[6..])
            : null;
    }

    private static ConsoleKey? Named(string name)
    {
        if (name.Length > 1 &&
            (name[0] == 'f' || name[0] == 'F') &&
            int.TryParse(name[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var number) &&
            number is >= 1 and <= 12)
        {
            return ConsoleKey.F1 + (number - 1);
        }

        return name.ToLowerInvariant() switch
        {
            "enter" => ConsoleKey.Enter,
            "esc" => ConsoleKey.Escape,
            "tab" => ConsoleKey.Tab,
            "space" => ConsoleKey.Spacebar,
            "up" => ConsoleKey.UpArrow,
            "down" => ConsoleKey.DownArrow,
            "left" => ConsoleKey.LeftArrow,
            "right" => ConsoleKey.RightArrow,
            "home" => ConsoleKey.Home,
            "end" => ConsoleKey.End,
            "pageup" => ConsoleKey.PageUp,
            "pagedown" => ConsoleKey.PageDown,
            "backspace" => ConsoleKey.Backspace,
            "delete" => ConsoleKey.Delete,
            "insert" => ConsoleKey.Insert,
            _ => null,
        };
    }

    private static KeyPress Typed(string name, KeyModifiers modifiers)
    {
        var character = name.Length > 0 ? name[0] : ' ';
        var key = char.IsAsciiLetter(character)
            ? Enum.Parse<ConsoleKey>(char.ToUpperInvariant(character).ToString())
            : char.IsAsciiDigit(character)
                ? ConsoleKey.D0 + (character - '0')
                : default;

        return new(key, modifiers, character);
    }
}
