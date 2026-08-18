using System;
using Arlecchino.Editing;
using Arlecchino.Hosting;
using Arlecchino.Input;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// The search that runs while you type, which moves the cursor to the first name beginning with what has
/// been spelled. It is a line of text like any other while it has the keyboard.
/// </summary>
internal sealed class SearchLine
{
    private readonly ArlecchinoKeymap _keymap;
    private readonly KeyText _keys;
    private readonly IArlecchinoTerminal _terminal;
    private readonly Action<string> _look;

    private bool _running;

    /// <summary>Puts a search over a panel.</summary>
    /// <param name="keymap">The keys the application obeys, which the line is edited by.</param>
    /// <param name="keys">
    /// Turns a key press into the character it types, so a name is spelled the same with a Cyrillic
    /// layout switched on.
    /// </param>
    /// <param name="terminal">Reached for the clipboard when what is spelled is copied or cut.</param>
    /// <param name="look">What to do with the letters so far, which is to go and find them.</param>
    public SearchLine(ArlecchinoKeymap keymap, KeyText keys, IArlecchinoTerminal terminal, Action<string> look)
    {
        _keymap = keymap;
        _keys = keys;
        _terminal = terminal;
        _look = look;
    }

    /// <summary>Whether the search has the keyboard, in which case typing is its own.</summary>
    public bool IsRunning => _running;

    /// <summary>What is being spelled, with the caret and whatever is selected in it.</summary>
    public TextEntry Entry { get; } = new();

    /// <summary>Whatever has been typed into it, which the foot of the panel shows.</summary>
    public string Text => Entry.Text;

    /// <summary>Starts it, with nothing spelled yet.</summary>
    public void Start()
    {
        _running = true;
        Entry.Text = "";
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

        TextEditing.InsertText(Entry, PastedText.FirstLine(text, static character => !char.IsControl(character)));
        _look(Entry.Text);

        return true;
    }

    /// <summary>
    /// Reads one key. Everything a line of text answers to is its own while something is spelled, and
    /// anything else ends the search, apart from Escape, which it keeps.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when the search took it.</returns>
    public bool Handle(KeyPress key)
    {
        if (!_running)
        {
            return false;
        }

        if (Entry.Text.Length > 0 && EntryKeys.Handled(Entry, _keymap, _terminal.CopyToClipboard, key))
        {
            _look(Entry.Text);

            return true;
        }

        if (key.Modifiers.HasFlag(KeyModifiers.Control) ||
            key.Modifiers.HasFlag(KeyModifiers.Alt) ||
            key.Modifiers.HasFlag(KeyModifiers.Super) ||
            _keys.Resolve(key) is not { } text ||
            char.IsControl(text))
        {
            _running = false;

            return key.Key is ConsoleKey.Escape;
        }

        TextEditing.Insert(Entry, text);
        _look(Entry.Text);

        return true;
    }
}
