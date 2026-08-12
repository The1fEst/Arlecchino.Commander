using System;
using Arlecchino.Commander.Model;
using Arlecchino.Input;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// The search that runs while you type, which moves the cursor to the first name beginning with what has
/// been spelled so far. It keeps the letters and hands everything else back to the panel.
/// </summary>
internal sealed class SearchLine
{
    private readonly KeyText _keys;
    private readonly Action<string> _look;

    private string _typed = "";
    private bool _running;

    /// <summary>Puts a search over a panel.</summary>
    /// <param name="keys">
    /// Turns a key press into the character it types, so a name is spelled the same with a Cyrillic
    /// layout switched on.
    /// </param>
    /// <param name="look">What to do with the letters so far, which is to go and find them.</param>
    public SearchLine(KeyText keys, Action<string> look)
    {
        _keys = keys;
        _look = look;
    }

    /// <summary>Whether the search has the keyboard, in which case typing is its own.</summary>
    public bool IsRunning => _running;

    /// <summary>Whatever has been typed into it, which the foot of the panel shows.</summary>
    public string Typed => _typed;

    /// <summary>Starts it, with nothing spelled yet.</summary>
    public void Start()
    {
        _running = true;
        _typed = "";
    }

    /// <summary>
    /// Adds pasted text to what is being spelled. It is taken only while the search has the keyboard:
    /// with the panel itself listening, letters are keys, and what was on the clipboard was meant for the
    /// command line instead.
    /// </summary>
    /// <param name="text">What was pasted.</param>
    /// <returns><c>true</c> when the search took it.</returns>
    public bool Paste(string text)
    {
        if (!_running)
        {
            return false;
        }

        Spell(Pasted.OneLine(text));

        return true;
    }

    /// <summary>
    /// Reads one key. Anything that is not a letter or a rub-out ends the search and is left for the panel,
    /// apart from Escape, which is kept so calling a search off does not also leave the screen.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when the search took it.</returns>
    public bool Handle(KeyPress key)
    {
        if (!_running)
        {
            return false;
        }

        if (key.Key is ConsoleKey.Backspace && _typed.Length > 0)
        {
            _typed = _typed[..^1];
            _look(_typed);

            return true;
        }

        if (key.Modifiers.HasFlag(KeyModifiers.Control) ||
            key.Modifiers.HasFlag(KeyModifiers.Alt) ||
            key.Modifiers.HasFlag(KeyModifiers.Super) ||
            _keys.Resolve(key) is not { } typed ||
            char.IsControl(typed))
        {
            _running = false;

            return key.Key is ConsoleKey.Escape;
        }

        Spell(typed.ToString());

        return true;
    }

    private void Spell(string more)
    {
        _typed += more;

        _look(_typed);
    }
}
