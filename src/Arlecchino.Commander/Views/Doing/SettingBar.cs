using System;
using System.Collections.Generic;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Rendering;
using Arlecchino.State;

namespace Arlecchino.Commander.Views.Doing;

/// <summary>
/// The line settings are typed on, and the box of what could be typed next that stands over it.
///
/// It is the command line's row and the command line's editing, opened by an exclamation mark instead of
/// a colon. A dialog was the other way to ask, and it is the wrong shape for this: a dialog asks one
/// question, and setting something is a name and a value and often a second thought about the value. The
/// row narrows as it is typed into and says what each name means while you are still deciding, which is
/// the difference between remembering what a setting is called and finding out.
///
/// Nothing here knows what any setting does. The names, what they are for and what is worth suggesting
/// all come from <see cref="Settings"/>, so a setting added there is on this line without a word being
/// written here.
/// </summary>
public sealed class SettingBar
{
    private const char Opener = '!';

    private readonly CommandLine _line;
    private readonly Settings _settings;
    private readonly ArlecchinoState _state;
    private readonly ArlecchinoKeymap _keymap;
    private readonly List<string> _history = [];

    /// <summary>Puts the line over the settings it changes.</summary>
    /// <param name="settings">What can be set, and what each of them is now.</param>
    /// <param name="state">Where what happened is said.</param>
    /// <param name="keymap">Keys to obey.</param>
    /// <param name="keys">
    /// Turns a key press into the character it types, so a setting is typed the same with a Cyrillic
    /// layout left switched on.
    /// </param>
    public SettingBar(Settings settings, ArlecchinoState state, ArlecchinoKeymap keymap, KeyText keys)
    {
        _settings = settings;
        _state = state;
        _keymap = keymap;
        _line = new(_history, keys, keymap, Opener);
    }

    /// <summary>Whether the line has the keyboard, which is what the panel asks before reading a letter.</summary>
    public bool IsTyping => _line.IsTyping;

    /// <summary>Asks for the line, as the menu entry does when there is no key being pressed.</summary>
    public void Open() => _line.Open();

    /// <summary>Draws the line, in the row the command line would have had.</summary>
    /// <param name="row">The row to draw on.</param>
    public void Draw(SurfaceRegion row) =>
        _line.Draw(row, Loc(LocString.MenuSettings), Loc(LocString.SettingsTail));

    /// <summary>Draws the box of what could be typed next, or nothing when nobody is typing.</summary>
    /// <param name="over">Everything above the line.</param>
    public void DrawHints(SurfaceRegion over)
    {
        if (_line.IsTyping)
        {
            SettingHints.Draw(over, Loc(LocString.MenuSettings), Hints());
        }
    }

    /// <summary>
    /// Gives the key to the line, which takes it only once it has been asked for. Tab finishes the word
    /// being typed and Enter keeps what was typed; everything else is the editing every line does.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when the line took it.</returns>
    public bool Handle(KeyPress key)
    {
        if (_line.Opens(key))
        {
            _line.Open();

            return true;
        }

        if (!_line.IsTyping)
        {
            return false;
        }

        if (key.Key == ConsoleKey.Tab)
        {
            Complete();

            return true;
        }

        if (_line.IsEmpty || !_keymap.Confirm.Matches(key))
        {
            return _line.Handle(key);
        }

        Enter();

        return true;
    }

    /// <summary>Puts pasted text on the line, but only while it is the line being typed on.</summary>
    /// <param name="text">What was pasted.</param>
    /// <returns><c>true</c> when the line took it.</returns>
    public bool Paste(string text)
    {
        if (!_line.IsTyping)
        {
            return false;
        }

        _line.Paste(text);

        return true;
    }

    /// <summary>
    /// Keeps what was typed. A name on its own fills in what that setting is now and leaves the line
    /// open. It is the way to see a value before changing it, and it is what the eye expects from a row
    /// that has been narrowing towards one name. A name and a value together is the change itself.
    ///
    /// A name nothing answers to leaves the line as it was. What was typed is a word away from being
    /// right, and closing the row would mean typing all of it again.
    /// </summary>
    private void Enter()
    {
        var (name, value) = Split(_line.Typed);

        if (_settings.Named(name) is not { } setting)
        {
            _state.Output = Loc(LocString.SaidNoSetting, name);

            return;
        }

        if (value.Length == 0)
        {
            _line.Set($"{setting.Name} {_settings.Value(setting.Name)}");

            return;
        }

        var kept = _settings.Set(setting.Name, value);

        _history.Add($"{setting.Name} {value}");
        _line.Close();

        _state.Output = kept
            ? Loc(LocString.SaidSettingKept, setting.Name, value)
            : Loc(LocString.SaidSettingNotWritten, setting.Name, value, _settings.Place);
    }

    /// <summary>Finishes the half-typed word from the first thing the box is offering for it.</summary>
    private void Complete()
    {
        if (Hints() is not [{ } first, ..])
        {
            return;
        }

        var (name, value) = Split(_line.Typed);

        _line.Set(_line.Typed.Contains(' ', StringComparison.Ordinal) || value.Length > 0
            ? $"{name} {first.Word}"
            : $"{first.Word} ");
    }

    /// <summary>
    /// What the box is offering. Before a space it is the settings whose names begin with what has been
    /// typed; after one it is what the named setting is worth being set to, narrowed the same way. So the
    /// same gesture — keep typing — narrows first the name and then the value.
    /// </summary>
    /// <returns>The rows, or nothing when what is typed can go nowhere.</returns>
    private List<SettingHint> Hints()
    {
        var typed = _line.Typed;
        var rows = new List<SettingHint>();

        if (!typed.Contains(' ', StringComparison.Ordinal))
        {
            foreach (var setting in _settings.All)
            {
                if (setting.Name.StartsWith(typed.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    rows.Add(new(setting.Name, Shown(setting.Name), Loc(setting.About)));
                }
            }

            return rows;
        }

        var (name, half) = Split(typed);

        if (_settings.Named(name) is not { } named)
        {
            return rows;
        }

        foreach (var suggestion in named.Suggest())
        {
            if (suggestion.StartsWith(half, StringComparison.OrdinalIgnoreCase))
            {
                rows.Add(new(suggestion, "", ""));
            }
        }

        return rows;
    }

    /// <summary>What a setting is now, said in a way that reads as an answer when it is nothing at all.</summary>
    /// <param name="name">Which setting.</param>
    /// <returns>Its value, or the words for having none.</returns>
    private string Shown(string name) =>
        _settings.Value(name) is { Length: > 0 } value ? value : Loc(LocString.SettingsUnset);

    /// <summary>What was typed, as the name and whatever follows it.</summary>
    /// <param name="typed">The line.</param>
    /// <returns>The name and the value, with the space between them gone.</returns>
    private static (string Name, string Value) Split(string typed)
    {
        var space = typed.IndexOf(' ', StringComparison.Ordinal);

        return space < 0 ? (typed.Trim(), "") : (typed[..space].Trim(), typed[(space + 1)..].Trim());
    }
}
