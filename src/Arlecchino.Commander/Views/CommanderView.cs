using System;
using System.Collections.Generic;
using System.Linq;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Views.Doing;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Commander.Widgets.Dialogs;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Commands;
using Arlecchino.Focus;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Layout;
using Arlecchino.Navigation;
using Arlecchino.Rendering;
using Arlecchino.State;
using Microsoft.Extensions.Hosting;
using static Arlecchino.Layout.PaneSplit;
using static Arlecchino.Layout.PaneTree;

namespace Arlecchino.Commander.Views;

/// <summary>
///     The screen the application opens on: two panels, a band above them, a command line and a bar of
///     keys below. It draws and routes; what every key does lives in <see cref="Doings" />.
/// </summary>
public sealed class CommanderView : IArlecchinoView, IDisposable
{
    private readonly ActionBar _actionBar;
    private readonly JobCard _card;
    private readonly IReadOnlyList<ViewCommand> _commands;
    private readonly IReadOnlyList<ViewCommand> _typed;
    private readonly Doings _doings;
    private readonly Gutter _gutter;
    private readonly ArlecchinoKeymap _keymap;
    private readonly KeyText _typing;
    private readonly CommandKeys _keys;
    private readonly Operations _operations;

    private readonly Pair _panels;
    private readonly Dictionary<Session, (FilePanel Left, FilePanel Right)> _panes = [];
    private readonly Sessions _sessions;

    private readonly Surface _surface;
    private readonly CommandBar _commandBar;
    private readonly SettingBar _settingBar;

    private readonly IDisposable _actionBarHeight;

    private FocusRing _focus;
    private PaneTree _layout;
    private int _moved;
    private int _seen;
    private Session _showing;

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
    /// <param name="services">Where the navigator is found, which is built after this screen is.</param>
    /// <param name="lifetime">How the application is quit.</param>
    /// <param name="settings">What is kept between runs, which the settings line changes.</param>
    /// <param name="keys">Asked what a half-typed chord has behind it, which this screen draws itself.</param>
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
        IServiceProvider services,
        IHostApplicationLifetime lifetime,
        Settings settings,
        CommandKeys keys)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(options);

        _surface = surface;
        _sessions = sessions;
        _operations = operations;
        _keymap = options.Keymap;
        _typing = KeyText.For(options.TextInput);
        _keys = keys;

        _seen = operations.Revision.Value;
        _moved = sessions.Revision.Value;
        _showing = sessions.Current;

        var (left, right) = Panes(_showing);
        var dialogs = new Dialogs(state);

        _panels = new(left, right);
        _settingBar = new(settings, state, _keymap, _typing);
        _doings = new(
            dialogs,
            _panels,
            sessions,
            operations,
            runner,
            finder,
            remote,
            state,
            terminal,
            services,
            settings,
            _settingBar);
        _commandBar = new(new(runner.History, _typing, _keymap, ':'), runner, state, _keymap, _panels);
        _gutter = new(sessions, _panels);
        _actionBar = new(_panels);
        _card = new(runner, state);
        _commands = CommanderKeys.For(
            _doings,
            _panels,
            sessions,
            operations,
            runner,
            _commandBar,
            _settingBar,
            lifetime);
        _typed = [.. _commands.Where(command => !WantedByTheLine(command.Binding))];

        _actionBarHeight = _actionBar.Height.Subscribe(() => _layout = Lay());
        _layout = Lay();
        _focus = _layout.AsFocusRing(_keymap);

        _focus.Focus(sessions.RightIsActive.Value ? right : left);
    }

    /// <summary>
    ///     What the panels have to give up at the foot: the command line, which is always one row, and
    ///     however many rows the bar of keys wrapped itself into.
    /// </summary>
    private int FooterRows => 1 + _actionBar.Height.Value;

    /// <summary>
    ///     Draws the screen, reloading the panels first when work that was running elsewhere has finished
    ///     since the last frame — the operation outlives this screen, so the screen catches up rather than
    ///     being told.
    /// </summary>
    public void Draw()
    {
        Showing();

        if (_seen != _operations.Revision.Value)
        {
            _seen = _operations.Revision.Value;

            _panels.Active.State.Marks.Clear();
            _panels.Left.Reload();
            _panels.Right.Reload();
        }

        if (_moved != _sessions.Revision.Value)
        {
            _moved = _sessions.Revision.Value;

            _panels.Left.Reload();
            _panels.Right.Reload();
        }

        var screen = _surface.Content;
        var above = screen.Rows(0, Math.Max(0, screen.Height - FooterRows));

        _layout.Draw(screen);
        _card.Draw(above);
        _settingBar.DrawHints(above);

        if (_keys.IsWaiting)
        {
            KeyHints.Draw(above, Loc(LocString.MenuKeys), _keys.Hints());
        }
    }

    /// <summary>
    ///     The one row under the panels, which belongs to whichever line has been asked for. Two lines
    ///     cannot both have it, and they never want it at once.
    /// </summary>
    /// <param name="row">The row to draw on.</param>
    private void Line(SurfaceRegion row)
    {
        if (_settingBar.IsTyping)
        {
            _settingBar.Draw(row);

            return;
        }

        _commandBar.Draw(row);
    }

    /// <summary>
    ///     Keys the screen itself takes before the panels see them, which is everything the command line
    ///     claims while there is something typed on it.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns>Where to go, which is nowhere for all of these.</returns>
    public ViewRoute Handle(KeyPress key)
    {
        if (!_panels.Active.IsSearching && Typed(key))
        {
            return ViewRoute.None;
        }

        return Routed(_focus.Handle(key));
    }

    /// <summary>
    ///     Offers the key to the two lines under the panels. Whichever of them has the keyboard is asked
    ///     first and asked alone; with neither open the key goes to both.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when a line took it.</returns>
    private bool Typed(KeyPress key)
    {
        if (_commandBar.IsTyping)
        {
            return _commandBar.Handle(key);
        }

        return _settingBar.IsTyping ? _settingBar.Handle(key) : _settingBar.Handle(key) || _commandBar.Handle(key);
    }

    /// <summary>
    ///     Text pasted into the terminal, which goes where typing goes: the search running on the panel
    ///     when there is one, and the lines under the panels otherwise.
    /// </summary>
    /// <param name="text">What was pasted, with the terminal's markers already stripped.</param>
    /// <returns>Where to go, which is nowhere.</returns>
    public ViewRoute HandlePaste(string text)
    {
        if (!_panels.Active.Paste(text) && !_settingBar.Paste(text))
        {
            _commandBar.Paste(text);
        }

        return ViewRoute.None;
    }

    /// <summary>
    ///     Clicks, which go to whichever panel was clicked in. The band along the top belongs to the
    ///     layout, which is asked before the view is.
    /// </summary>
    /// <param name="mouse">The event that arrived.</param>
    /// <returns>Where to go, which is nowhere.</returns>
    public ViewRoute HandleMouse(MouseEvent mouse)
    {
        return Routed(_focus.HandleMouse(mouse));
    }

    /// <summary>
    ///     Every key the screen answers to, minus the ones a letter could be while something is being
    ///     typed into. What is left then is the function keys and everything held with a modifier.
    /// </summary>
    /// <returns>The commands to match this key against.</returns>
    public IReadOnlyList<ViewCommand> Commands()
    {
        return _commandBar.IsTyping || _settingBar.IsTyping || _panels.Active.IsSearching ? _typed : _commands;
    }

    /// <summary>
    ///     Whether a line of text would rather have this key. Anything pressed on its own it would: the
    ///     letters it types, and the arrows and the rub-outs it edits with.
    /// </summary>
    /// <param name="binding">The binding to judge.</param>
    /// <returns><c>true</c> when the line should be given the key instead.</returns>
    private static bool WantedByTheLine(KeyBinding binding) =>
        binding is { Modifiers: KeyModifiers.None, Key: < ConsoleKey.F1 or > ConsoleKey.F24 };

    /// <summary>
    ///     The two panels of a session, made once and kept, so a tab that is come back to shows what it
    ///     showed before.
    /// </summary>
    /// <param name="session">Whose panels.</param>
    /// <returns>The pair.</returns>
    private (FilePanel Left, FilePanel Right) Panes(Session session)
    {
        if (_panes.TryGetValue(session, out var made))
        {
            return made;
        }

        FilePanel Over(PanelState state)
        {
            return new(state, _keymap, _typing)
            {
                OnOpenFile = entry => _doings.Open(entry),
                OnGroup = marking => _doings.Group(marking)
            };
        }

        made = (Over(session.Left), Over(session.Right));
        _panes[session] = made;

        return made;
    }

    /// <summary>
    ///     Lays the screen out: the two panels side by side with the gutter between them, and the line and
    ///     the bar of keys under both. Both splits are measured from the end.
    /// </summary>
    /// <returns>The layout, which the focus ring is made from as well as drawn.</returns>
    private PaneTree Lay()
    {
        return Branch(
            Rows,
            PaneSize.CellsFromEnd(FooterRows),
            Branch(
                Columns,
                PaneSize.Fraction(0.5),
                Leaf(_panels.Left),
                Branch(
                    Columns,
                    PaneSize.Cells(Gutter.Width),
                    Leaf(_gutter.Draw),
                    Leaf(_panels.Right))),
            Branch(
                Rows,
                PaneSize.CellsFromEnd(_actionBar.Height.Value),
                Leaf(Line),
                Leaf(_actionBar.Draw)));
    }

    /// <summary>
    ///     Swaps the panels over when the tab has changed. The layout holds the two it was built with, so a
    ///     new tab means a new layout and a focus ring to go with it.
    /// </summary>
    private void Showing()
    {
        if (ReferenceEquals(_showing, _sessions.Current))
        {
            return;
        }

        _showing = _sessions.Current;

        var (left, right) = Panes(_showing);

        _panels.Show(left, right);

        _layout = Lay();
        _focus = _layout.AsFocusRing(_keymap);

        _focus.Focus(_sessions.RightIsActive.Value ? right : left);
    }

    /// <summary>Remembers which side the focus landed on, so the tab comes back as it was left.</summary>
    /// <param name="route">Where whatever took the event wants to go.</param>
    /// <returns>The same.</returns>
    private ViewRoute Routed(ViewRoute route)
    {
        _sessions.RightIsActive.Value = _panels.Right.IsFocused;

        return route;
    }

    /// <summary>
    ///     Gives up what watching the bar's height took out. The screen is built afresh every time it is
    ///     navigated back to, so a subscription that outlived it would be one more per visit.
    /// </summary>
    public void Dispose()
    {
        _actionBarHeight.Dispose();
    }
}
