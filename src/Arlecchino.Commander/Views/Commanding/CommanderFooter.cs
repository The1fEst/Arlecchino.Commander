using System;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Views.Doing;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Rendering;
using Arlecchino.State;

namespace Arlecchino.Commander.Views.Commanding;

/// <summary>
///     What sits under the panels: the rows whichever line has been asked for takes, and the bar of keys
///     below them. The two lines cannot both have the room, and they never want it at once.
/// </summary>
public sealed class CommanderFooter
{
    private readonly ActionBar _actionBar;

    /// <summary>Builds the two lines and the bar.</summary>
    /// <param name="runner">What runs commands, whose history the command line steps through.</param>
    /// <param name="state">Where the dialog on top and the last word said live.</param>
    /// <param name="settings">What is kept between runs, which the settings line changes.</param>
    /// <param name="keymap">Keys the lines obey.</param>
    /// <param name="typing">Turns a key press into the character it types.</param>
    /// <param name="terminal">What reaches the clipboard, for the selection that is copied or cut.</param>
    /// <param name="panels">The two panels on screen, which the command line writes the names from.</param>
    /// <param name="keyboard">Every key the screen answers to, which the bar names ten of.</param>
    public CommanderFooter(
        Runner runner,
        ArlecchinoState state,
        Settings settings,
        ArlecchinoKeymap keymap,
        KeyText typing,
        IArlecchinoTerminal terminal,
        Pair panels,
        Keyboard keyboard)
    {
        Setting = new(settings, state, keymap, typing, terminal);
        Line = new(new(runner.History, typing, keymap, terminal, ':'), runner, state, keymap, panels);
        _actionBar = new(keyboard);
    }

    /// <summary>The line commands are typed on.</summary>
    public CommandBar Line { get; }

    /// <summary>The line settings are typed on.</summary>
    public SettingBar Setting { get; }

    /// <summary>
    ///     What the panels have to give up at the foot: the rows the line being typed on carried itself onto,
    ///     and however many rows the bar of keys wrapped itself into.
    /// </summary>
    public int Rows => LineRows + BarRows;

    /// <summary>How many rows the bar of keys took, which is known only once it has been drawn.</summary>
    public int BarRows => _actionBar.Height.Value;

    /// <summary>
    ///     How many rows the line took. It is the line that is being drawn that is asked, since the other one
    ///     is left holding whatever it took when it last had the room.
    /// </summary>
    public int LineRows => Setting.IsTyping ? Setting.Height.Value : Line.Height.Value;

    /// <summary>Whether either line has the keyboard.</summary>
    public bool IsTyping => Line.IsTyping || Setting.IsTyping;

    /// <summary>
    ///     Says when the bar or either line wrapped onto another row, which the screen is laid out again after.
    /// </summary>
    /// <param name="then">What to do about it.</param>
    /// <returns>What to give up to stop hearing about it.</returns>
    public IDisposable[] WhenResized(Action then)
    {
        return [_actionBar.Height.Subscribe(then), Line.Height.Subscribe(then), Setting.Height.Subscribe(then)];
    }

    /// <summary>Draws whichever line has been asked for.</summary>
    /// <param name="row">The rows to draw on.</param>
    public void DrawLine(SurfaceRegion row)
    {
        if (Setting.IsTyping)
        {
            Setting.Draw(row);

            return;
        }

        Line.Draw(row);
    }

    /// <summary>Draws the bar of keys.</summary>
    /// <param name="bar">The rows to draw on.</param>
    public void DrawBar(SurfaceRegion bar)
    {
        _actionBar.Draw(bar);
    }

    /// <summary>
    ///     Draws what the line being typed on is offering, which stands over the panels. Only one line is
    ///     ever typed on, so only one box is ever drawn.
    /// </summary>
    /// <param name="over">The room above the foot.</param>
    public void DrawHints(SurfaceRegion over)
    {
        if (Setting.IsTyping)
        {
            Setting.DrawHints(over);

            return;
        }

        Line.DrawHints(over);
    }

    /// <summary>
    ///     Offers the key to the two lines. Whichever of them has the keyboard is asked first and asked
    ///     alone; with neither open the key goes to both.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when a line took it.</returns>
    public bool Handle(KeyPress key)
    {
        if (Line.IsTyping)
        {
            return Line.Handle(key);
        }

        return Setting.IsTyping ? Setting.Handle(key) : Setting.Handle(key) || Line.Handle(key);
    }

    /// <summary>
    ///     Offers pasted text to the two lines. The settings line takes it only while it is open, so what is
    ///     left over goes on the command line.
    /// </summary>
    /// <param name="text">What was pasted, with the terminal's markers already stripped.</param>
    public void Paste(string text)
    {
        if (Setting.Paste(text))
        {
            return;
        }

        Line.Paste(text);
    }
}
