using System;
using System.Collections.Generic;
using Arlecchino.Commander.Stores;
using Arlecchino.Commands;
using Arlecchino.Focus;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Layout;
using Arlecchino.Navigation;
using Arlecchino.Rendering;
using Arlecchino.State;
using Arlecchino.Widgets.Lists;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Widgets.Readouts;
using static Arlecchino.Layout.PaneSplit;
using static Arlecchino.Layout.PaneTree;

namespace Arlecchino.Commander.Views;

/// <summary>
/// What the search turned up. Enter takes the panel to a result and puts the cursor on it, which is
/// what a list of files found is for.
/// </summary>
public sealed class FindView : IArlecchinoView
{
    private readonly Surface _surface;
    private readonly Finder _finder;
    private readonly Sessions _sessions;
    private readonly ArlecchinoState _state;
    private readonly Spinner _spinner = new();
    private readonly PaneTree _layout;
    private readonly FocusRing _focus;

    public FindView(Surface surface, Finder finder, Sessions sessions, ArlecchinoState state, ArlecchinoOptions options)
    {
        _surface = surface;
        _finder = finder;
        _sessions = sessions;
        _state = state;

        var hits = new ListBox<Hit>(options.Keymap)
        {
            Render = Under,
            ItemStyle = static _ => Skin.Terminal.Text,
            OnActivate = Open,
            Items = finder.Found.Value,
        };

        _layout = Branch(
            Rows,
            Sheet.Head,
            Leaf(DrawHeader),
            Branch(Rows, PaneSize.CellsFromEnd(Sheet.Foot), Leaf(hits), Leaf(DrawFooter)));

        _focus = _layout.AsFocusRing(options.Keymap);
    }

    public void Draw() => _layout.Draw(_surface.Content);

    public ViewRoute Handle(ConsoleKeyInfo key) => _focus.Handle(key);

    public ViewRoute HandleMouse(MouseEvent mouse) => _focus.HandleMouse(mouse);

    public IReadOnlyList<ViewCommand> Commands() =>
    [
        Bind.Going(new(ConsoleKey.Escape), LocString.KeyBack, static () => ViewKind.Commander),
        Bind.When(new(ConsoleKey.F3), LocString.FindStop, () => _finder.IsRunning, Stop),
    ];

    private ViewRoute Stop()
    {
        _finder.Stop();

        return ViewRoute.None;
    }

    private void DrawHeader(SurfaceRegion header)
    {
        Sheet.Title(header, _finder.What, Loc(LocString.FindFoundIn, _finder.Found.Count, _finder.Looked));

        if (!_finder.IsRunning)
        {
            return;
        }

        _spinner.Advance();
        _spinner.Draw(header.SplitLeft(header.Width - 1).Right);
    }

    private void DrawFooter(SurfaceRegion footer) => Sheet.Hints(footer, Said(), Loc(LocString.FindHints));

    /// <summary>
    /// A result as it is listed: the path below the folder the search started from, because every
    /// one of them begins with that folder and repeating it leaves no room for the rest.
    /// </summary>
    /// <param name="hit">The result.</param>
    /// <returns>The line to show.</returns>
    private string Under(Hit hit)
    {
        var root = _finder.Root;
        var folder = hit.Folder.Length > root.Length && hit.Folder.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? hit.Folder[root.Length..].TrimStart('/', '\\')
            : hit.Folder;

        return folder.Length == 0 ? hit.Entry.Name : Loc(LocString.Joined, folder, hit.Entry.Name);
    }

    private string Said() => _finder.IsRunning
        ? Loc(LocString.FindSearching, _spinner.Current)
        : _finder.Found.Count == 0
            ? Loc(LocString.FindNothing)
            : Loc(LocString.FindFound, _finder.Found.Count);

    /// <summary>
    /// Sends the panel that was active to the folder a result is in, with the cursor on the file
    /// itself. The panel is left to reload on its own when the screen comes back.
    /// </summary>
    /// <param name="hit">The result that was chosen.</param>
    /// <returns>The screen with the panels on it.</returns>
    private ViewRoute Open(Hit hit)
    {
        var panel = _sessions.RightIsActive.Value ? _sessions.Right : _sessions.Left;

        panel.GoTo(hit.Folder);
        panel.Cursor = hit.Entry.Name;

        _sessions.Moved();
        _state.Output = hit.Entry.Path;

        return ViewKind.Commander;
    }
}
