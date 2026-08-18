using System;
using System.Collections.Generic;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Views.Commanding;
using Arlecchino.Commander.Views.Doing;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Commands;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Navigation;
using Arlecchino.Rendering;
using Arlecchino.State;
using Microsoft.Extensions.Hosting;

namespace Arlecchino.Commander.Views;

/// <summary>
///     The screen the application opens on: two panels, a command line and a bar of keys below them. It puts
///     the parts together and draws what stands over them; where everything sits is
///     <see cref="CommanderLayout" />, and what every key does is <see cref="Doings" />.
/// </summary>
public sealed class CommanderView : IArlecchinoView, IDisposable
{
    private readonly JobCard _card;
    private readonly CommanderCommands _commands;
    private readonly CommanderFooter _footer;
    private readonly CommanderInput _input;
    private readonly CommandKeys _keys;
    private readonly CommanderLayout _layout;
    private readonly CommanderPanels _panels;
    private readonly Surface _surface;

    /// <summary>Builds the screen over whichever tab was open.</summary>
    /// <param name="surface">What is drawn on.</param>
    /// <param name="sessions">Every tab and which one is open.</param>
    /// <param name="remote">Where the connection that was made is remembered.</param>
    /// <param name="operations">What carries file work out.</param>
    /// <param name="runner">What runs commands.</param>
    /// <param name="finder">What walks a folder looking for something.</param>
    /// <param name="state">Where the dialog on top and the last word said live.</param>
    /// <param name="options">The keys and the terminal this was started with.</param>
    /// <param name="terminal">What reaches the clipboard of the machine the user is sitting at.</param>
    /// <param name="navigation">Where the other screens of the application are reached.</param>
    /// <param name="handover">What lends the terminal to the editor while it runs.</param>
    /// <param name="lifetime">How the application is quit.</param>
    /// <param name="settings">What is kept between runs, which the settings line changes.</param>
    /// <param name="keys">Asked what a half-typed chord has behind it, which this screen draws itself.</param>
    /// <param name="keyboard">Where this screen leaves its table of keys for the screen that lists them.</param>
    public CommanderView(
        Surface surface,
        Sessions sessions,
        Remote remote,
        Operations operations,
        Runner runner,
        Finder finder,
        ArlecchinoState state,
        ArlecchinoOptions options,
        IArlecchinoTerminal terminal,
        Navigator navigation,
        Handover handover,
        IHostApplicationLifetime lifetime,
        Settings settings,
        CommandKeys keys,
        Keyboard keyboard)
    {
        _surface = surface;
        _keys = keys;

        var typing = KeyText.For(options.TextInput);

        _panels = new(sessions, operations, settings, options.Keymap, typing, terminal);

        var panels = _panels.Panels;

        _footer = new(runner, state, settings, options.Keymap, typing, terminal, panels, keyboard);

        var doings = new Doings(
            new(state),
            panels,
            sessions,
            operations,
            runner,
            finder,
            remote,
            state,
            terminal,
            navigation,
            handover,
            settings,
            _footer.Setting);

        _panels.OnOpenFile = doings.Open;
        _panels.OnGroup = doings.Group;

        _card = new(runner, state);
        _layout = new(panels, sessions, _footer, options.Keymap);
        _input = new(panels, _footer, _layout, sessions);
        _commands = new(doings, panels, sessions, operations, runner, _footer, lifetime, keyboard);

        _layout.Focus(_panels.Working);
    }

    /// <summary>
    ///     Draws the screen: the tab that is open, whatever has happened to the files since the last frame,
    ///     and then the layout with what stands over it.
    /// </summary>
    public void Draw()
    {
        if (_panels.Showing())
        {
            _layout.Rebuild();
            _layout.Focus(_panels.Working);
        }

        _panels.Refresh();

        var screen = _surface.Content;
        var header = screen.Rows(0, Math.Max(0, screen.Height - _footer.Rows));

        _layout.Draw(screen);
        _card.Draw(header);
        _footer.DrawHints(header);

        if (_keys.IsWaiting)
        {
            KeyHints.Draw(header, Loc(LocString.MenuKeys), _keys.Hints());
        }
    }

    /// <summary>
    ///     Keys the screen itself takes before the panels see them, which is everything the command line
    ///     claims while there is something typed on it.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns>Where to go, which is nowhere for all of these.</returns>
    public ViewRoute Handle(KeyPress key)
    {
        return _input.Handle(key);
    }

    /// <summary>Text pasted into the terminal, which goes where typing goes.</summary>
    /// <param name="text">What was pasted, with the terminal's markers already stripped.</param>
    /// <returns>Where to go, which is nowhere.</returns>
    public ViewRoute HandlePaste(string text)
    {
        return _input.HandlePaste(text);
    }

    /// <summary>Clicks, which go to whichever panel was clicked in.</summary>
    /// <param name="mouse">The event that arrived.</param>
    /// <returns>Where to go, which is nowhere.</returns>
    public ViewRoute HandleMouse(MouseEvent mouse)
    {
        return _input.HandleMouse(mouse);
    }

    /// <summary>
    ///     Whether a line or a search has the keyboard. It keeps the keys a caret moves by word on from
    ///     walking the screens instead, which is what they do while nothing is being typed.
    /// </summary>
    public bool IsTyping => _footer.IsTyping || _panels.Panels.Active.IsSearching;

    /// <summary>Every key the screen answers to at this moment.</summary>
    /// <returns>The commands to match this key against.</returns>
    public IReadOnlyList<ViewCommand> Commands()
    {
        return _commands.Now();
    }

    /// <summary>
    ///     Gives up what watching the bar's height took out, and the watches the panels had on the file
    ///     system. The screen is built afresh on every visit, so anything outliving it would be one more.
    /// </summary>
    public void Dispose()
    {
        _layout.Dispose();
        _panels.Dispose();
    }
}
