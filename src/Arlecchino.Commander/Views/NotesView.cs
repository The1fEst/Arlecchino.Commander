using System;
using System.Collections.Generic;
using System.Globalization;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Commander.Widgets.Dialogs;
using Arlecchino.Commands;
using Arlecchino.Diagnostics;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Layout;
using Arlecchino.Navigation;
using Arlecchino.Rendering;
using Arlecchino.State;
using Arlecchino.Widgets.Lists;
using static Arlecchino.Layout.PaneSplit;
using static Arlecchino.Layout.PaneTree;

namespace Arlecchino.Commander.Views;

/// <summary>
/// What the application has said lately, newest first, in this application's own frame.
///
/// The framework brings a screen of its own for this, and it is a good one; it is drawn in boxes,
/// which is the one thing this design does not have. So the route is taken over rather than the
/// framework changed — an application that wants a look of its own should be able to have it without
/// every other application built on the framework having to agree.
/// </summary>
public sealed class NotesView : IArlecchinoView
{
    private const int BarCells = 12;
    private const char Filled = '█';
    private const char Empty = '░';

    private readonly Surface _surface;
    private readonly ArlecchinoState _state;
    private readonly ArlecchinoKeymap _keymap;
    private readonly Navigator _navigator;
    private readonly Dialogs _dialogs;
    private readonly ListBox<Notification> _list;
    private readonly PaneTree _layout;

    /// <summary>Builds the screen over what has been said.</summary>
    /// <param name="surface">What is drawn on.</param>
    /// <param name="state">Where the messages are kept.</param>
    /// <param name="options">The keys and the terminal this was started with.</param>
    /// <param name="navigator">How the screen is left.</param>
    public NotesView(Surface surface, ArlecchinoState state, ArlecchinoOptions options, Navigator navigator)
    {
        ArgumentNullException.ThrowIfNull(options);

        _surface = surface;
        _state = state;
        _navigator = navigator;
        _keymap = options.Keymap;
        _dialogs = new(state);

        _list = new(options.Keymap)
        {
            Render = Describe,
            OnActivate = Open,
            ItemStyle = static entry => entry.Loudness switch
            {
                NotificationLevel.Failure => Skin.Terminal.Warning,
                NotificationLevel.Warning => Skin.Terminal.Warning,
                _ => Skin.Terminal.Text,
            },
        };

        _layout = Branch(
            Rows,
            Sheet.Head,
            Leaf(DrawHeader),
            Branch(Rows, PaneSize.CellsFromEnd(Sheet.Foot), Leaf(DrawList), Leaf(DrawFooter)));
    }

    /// <inheritdoc/>
    public void Draw() => _layout.Draw(_surface.Content);

    /// <inheritdoc/>
    public ViewRoute Handle(ConsoleKeyInfo key)
    {
        if (_keymap.Cancel.Matches(key) || _keymap.Notifications.Matches(key))
        {
            _navigator.Back();

            return ViewRoute.None;
        }

        Listed();

        if (!_keymap.Erase.Matches(key))
        {
            return _list.Handle(key).Route;
        }

        _state.Notifications.Clear();

        return ViewRoute.None;
    }

    /// <inheritdoc/>
    public ViewRoute HandleMouse(MouseEvent mouse)
    {
        Listed();

        return _list.HandleMouse(mouse).Route;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ViewCommand> Commands() => [];

    private void DrawHeader(SurfaceRegion header) =>
        Sheet.Title(header, Loc(LocString.NotesTitle), Loc(LocString.NotesSaid));

    private void DrawFooter(SurfaceRegion footer) =>
        Sheet.Hints(footer, Counted(_state.Notifications.Entries.Count), Loc(LocString.NotesHints));

    private void DrawList(SurfaceRegion body)
    {
        var entries = Listed();

        if (entries.Count == 0)
        {
            body.Fill(Skin.Terminal.Text);
            body.Write(0, 0, Loc(LocString.NotesEmpty), Skin.Terminal.Meta);

            return;
        }

        _list.Draw(body);
    }

    /// <summary>
    /// Hands the list what is held right now. Keys are answered whether or not a frame has been drawn
    /// since the entry arrived, so this runs before drawing and before reading input rather than only
    /// on the way to the screen.
    /// </summary>
    /// <returns>What the list is showing.</returns>
    private IReadOnlyList<Notification> Listed()
    {
        var entries = _state.Notifications.Entries;

        _list.Items = entries;

        return entries;
    }

    /// <summary>
    /// Opens the entry that was confirmed. One row is not enough for a report, and the dialog it
    /// opens in is the one every other question in this application is asked through.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>Nowhere: reading a message does not leave the screen.</returns>
    private ViewRoute Open(Notification entry)
    {
        var wrong = entry.Loudness is NotificationLevel.Failure or NotificationLevel.Warning;

        _dialogs.Say(Loc(LocString.NotesTitle), entry.Whole(), wrong);

        return ViewRoute.None;
    }

    private static string Counted(int held) =>
        held == 1 ? Loc(LocString.NotesOne) : Loc(LocString.NotesMany, held);

    /// <summary>
    /// One row: the time it arrived, a small bar when the entry says how far along it is, and what it
    /// has to say.
    /// </summary>
    /// <param name="entry">The entry to describe.</param>
    /// <returns>The row.</returns>
    private static string Describe(Notification entry)
    {
        var stamp = entry.Time.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        if (entry.Filled() is not { } share)
        {
            return $"{stamp}  {entry.Line}";
        }

        var filled = (int)Math.Round(share * BarCells);
        var bar = new string(Filled, filled) + new string(Empty, Math.Max(0, BarCells - filled));

        return $"{stamp}  {bar} {share * 100:0}%  {entry.Line}";
    }
}
