using System;
using System.Collections.Generic;
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
///     keys below.
///     It draws and it routes. What every key and every menu entry actually does lives in
///     <see cref="Doings" />, which is handed the same pair of panels this screen is showing. So the screen
///     has no operation of its own to keep in step with the menu, and the menu has no idea which screen it
///     was opened from.
///     The footer is no longer a fixed two rows. The bar of keys wraps when the terminal is narrow, and it
///     only finds out how tall it is by drawing itself. So the screen subscribes to the height it reports and
///     lays itself out again when that changes, rather than guessing at a number a resize would make wrong.
/// </summary>
public sealed class CommanderView : IArlecchinoView, IDisposable
{
    private readonly ActionBar _actionBar;
    private readonly JobCard _card;
    private readonly IReadOnlyList<ViewCommand> _commands;
    private readonly Doings _doings;
    private readonly Gutter _gutter;
    private readonly ArlecchinoKeymap _keymap;
    private readonly KeyText _keys;
    private readonly Operations _operations;

    private readonly Pair _panels;
    private readonly Dictionary<Session, (FilePanel Left, FilePanel Right)> _panes = [];
    private readonly Sessions _sessions;
    private readonly ArlecchinoState _state;

    private readonly Surface _surface;
    private readonly CommandBar _commandBar;

    private readonly IDisposable _actionBarHeight;

    private FocusRing _focus;
    private PaneTree _layout;
    private int _moved;
    private bool _prefix;
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
        IHostApplicationLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(options);

        _surface = surface;
        _sessions = sessions;
        _state = state;
        _operations = operations;
        _keymap = options.Keymap;
        _keys = KeyText.For(options.TextInput);

        _seen = operations.Revision.Value;
        _moved = sessions.Revision.Value;
        _showing = sessions.Current;

        var (left, right) = Panes(_showing);
        var dialogs = new Dialogs(state);

        _panels = new(left, right);
        _doings = new(dialogs, _panels, sessions, operations, runner, finder, remote, state, terminal, services);
        _commandBar = new(new(runner.History, _keys, _keymap), runner, state, _keymap, _panels);
        _gutter = new(sessions, _panels);
        _actionBar = new(_panels);
        _card = new(runner, state);
        _commands = CommanderKeys.For(_doings, _panels, sessions, operations, runner, _commandBar, state, lifetime);

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

        _layout.Draw(screen);
        _card.Draw(screen.Rows(0, Math.Max(0, screen.Height - FooterRows)));
    }

    /// <summary>
    ///     Keys the screen itself takes before the panels see them: the second half of a <c>Ctrl+X</c>
    ///     pair, and everything the command line claims while there is something typed on it.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns>Where to go, which is nowhere for all of these.</returns>
    public ViewRoute Handle(ConsoleKeyInfo key)
    {
        if (_prefix)
        {
            _prefix = false;

            CommanderKeys.Prefixed(_doings, _commandBar, _state, key);

            return ViewRoute.None;
        }

        if (key is { Modifiers: ConsoleModifiers.Control, Key: ConsoleKey.X })
        {
            _prefix = true;
            _state.Output = Loc(LocString.PrefixHint);

            return ViewRoute.None;
        }

        if (!_panels.Active.IsSearching && _commandBar.Handle(key))
        {
            return ViewRoute.None;
        }

        return Routed(_focus.Handle(key));
    }

    /// <summary>
    ///     Clicks, which go to whichever panel was clicked in. The band along the top is no longer here — it
    ///     belongs to the layout, and the layout is asked before the view is — so everything that reaches
    ///     this has landed below it.
    /// </summary>
    /// <param name="mouse">The event that arrived.</param>
    /// <returns>Where to go, which is nowhere.</returns>
    public ViewRoute HandleMouse(MouseEvent mouse)
    {
        return Routed(_focus.HandleMouse(mouse));
    }

    /// <inheritdoc />
    public IReadOnlyList<ViewCommand> Commands()
    {
        return _commands;
    }

    /// <summary>
    ///     The two panels of a session, made once and kept. A tab that is come back to shows what it showed
    ///     before — the same cursor, the same place in a long folder — which it could not do if its panels
    ///     were built afresh every time it was switched to.
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
            return new(state, _keymap, _keys)
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
    ///     Lays the screen out: the two panels side by side with the gutter between them, and the command
    ///     line and the bar of keys under both. Both splits are measured from the end, so the footer keeps the
    ///     rows it asked for, and the panels take whatever is left over. That is the way round that survives
    ///     a terminal being made shorter.
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
                Leaf(_commandBar.Draw),
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
    ///     navigated back to, and the viewer, the finder and the output screen all leave it. A subscription
    ///     that outlived the screen would be one more per visit, each laying out a screen nobody is on.
    /// </summary>
    public void Dispose()
    {
        _actionBarHeight.Dispose();
    }
}
