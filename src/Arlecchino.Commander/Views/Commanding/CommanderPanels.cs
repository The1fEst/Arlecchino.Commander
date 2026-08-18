using System;
using System.Collections.Generic;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Navigation;

namespace Arlecchino.Commander.Views.Commanding;

/// <summary>
///     The panels behind the screen: one pair for every tab, made the first time that tab is looked at and
///     kept afterward, so a tab that is come back to shows what it showed before.
/// </summary>
public sealed class CommanderPanels : IDisposable
{
    private readonly ArlecchinoKeymap _keymap;
    private readonly Operations _operations;
    private readonly Dictionary<Session, (FilePanel Left, FilePanel Right)> _panes = [];
    private readonly Sessions _sessions;
    private readonly Settings _settings;
    private readonly KeyText _typing;
    private readonly IArlecchinoTerminal _terminal;
    private int _moves;
    private int _width;

    private Session _showing;

    /// <summary>Opens the panels of whichever tab is current.</summary>
    /// <param name="sessions">Every tab and which one is open.</param>
    /// <param name="operations">What carries file work out, whose count tells the panels to read again.</param>
    /// <param name="settings">What is kept between runs, which a panel reads itself by.</param>
    /// <param name="keymap">Keys the panels obey.</param>
    /// <param name="typing">Turns a key press into the character it types.</param>
    /// <param name="terminal">Reached for the clipboard by the search that runs while you type.</param>
    public CommanderPanels(
        Sessions sessions,
        Operations operations,
        Settings settings,
        ArlecchinoKeymap keymap,
        KeyText typing,
        IArlecchinoTerminal terminal)
    {
        _sessions = sessions;
        _operations = operations;
        _settings = settings;
        _keymap = keymap;
        _typing = typing;
        _terminal = terminal;

        _width = operations.Revision.Value;
        _moves = sessions.Revision.Value;
        _showing = sessions.Current;

        var (left, right) = Panes(_showing);

        Panels = new(left, right);
    }

    /// <summary>The two panels on screen, which the rest of the screen is built over.</summary>
    public Pair Panels { get; }

    /// <summary>
    ///     What opening a file on a panel comes to. It is set after the panels are made, because what the
    ///     screen can do is built over them.
    /// </summary>
    public Func<FileEntry, ViewRoute>? OnOpenFile { get; set; }

    /// <summary>What marking a group of files on a panel, or taking the marks off again, comes to.</summary>
    public Action<bool>? OnGroup { get; set; }

    /// <summary>The side the open tab was left on, which the focus belongs to.</summary>
    public FilePanel Working => _sessions.RightIsActive.Value ? Panels.Right : Panels.Left;

    /// <summary>
    ///     Gives up the watches the panels had on the file system. The screen is built afresh on every visit,
    ///     so anything outliving it would be one more.
    /// </summary>
    public void Dispose()
    {
        foreach (var (left, right) in _panes.Values)
        {
            left.Dispose();
            right.Dispose();
        }
    }

    /// <summary>
    ///     Puts the open tab's panels on screen when the tab has changed. The layout holds the two it was
    ///     built with, so the screen has to be laid out again after this says so.
    /// </summary>
    /// <returns><c>true</c> when the panels were swapped over.</returns>
    public bool Showing()
    {
        if (ReferenceEquals(_showing, _sessions.Current))
        {
            return false;
        }

        _showing = _sessions.Current;

        Panels.Left.IsShown = false;
        Panels.Right.IsShown = false;

        var (left, right) = Panes(_showing);

        Panels.Show(left, right);

        left.IsShown = true;
        right.IsShown = true;

        return true;
    }

    /// <summary>
    ///     Reads both panels again when work that was running elsewhere has finished, or when a folder was
    ///     moved to from somewhere other than the panel itself. The operation outlives the screen, so the
    ///     screen catches up rather than being told.
    /// </summary>
    public void Refresh()
    {
        if (_width != _operations.Revision.Value)
        {
            _width = _operations.Revision.Value;

            Panels.Active.State.Marks.Clear();
            Panels.Left.Reload();
            Panels.Right.Reload();
        }

        if (_moves == _sessions.Revision.Value)
        {
            return;
        }

        _moves = _sessions.Revision.Value;

        Panels.Left.Reload();
        Panels.Right.Reload();
    }

    /// <summary>The two panels of a session, made once and kept.</summary>
    /// <param name="session">Whose panels.</param>
    /// <returns>The pair.</returns>
    private (FilePanel Left, FilePanel Right) Panes(Session session)
    {
        if (_panes.TryGetValue(session, out var color))
        {
            return color;
        }

        FilePanel Built(PanelState state)
        {
            return new(state, _keymap, _typing, _terminal, _settings, _operations)
            {
                OnOpenFile = entry => OnOpenFile?.Invoke(entry) ?? ViewRoute.None,
                OnGroup = marking => OnGroup?.Invoke(marking)
            };
        }

        color = (Built(session.Left), Built(session.Right));
        _panes[session] = color;

        return color;
    }
}
