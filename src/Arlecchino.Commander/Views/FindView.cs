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
using Arlecchino.Rendering.Colors;
using Arlecchino.State;
using Arlecchino.Widgets.Lists;
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
    private const int HeaderRows = 2;

    private readonly Surface _surface;
    private readonly Finder _finder;
    private readonly Panels _panels;
    private readonly ArlecchinoState _state;
    private readonly Spinner _spinner = new();
    private readonly PaneTree _layout;
    private readonly FocusRing _focus;

    public FindView(Surface surface, Finder finder, Panels panels, ArlecchinoState state, ArlecchinoOptions options)
    {
        _surface = surface;
        _finder = finder;
        _panels = panels;
        _state = state;

        var hits = new ListBox<Hit>(options.Keymap)
        {
            Render = Under,
            ItemStyle = static _ => Theme.Default,
            OnActivate = Open,
            Items = finder.Found.Value,
        };

        var status = new StatusBar
        {
            Left = [Said],
            Right = [static () => "Enter goes there", static () => "Esc back"],
        };

        _layout = Branch(
            Rows,
            HeaderRows,
            Leaf(DrawHeader),
            Branch(Rows, PaneSize.CellsFromEnd(1), Leaf(hits, static () => "Found"), Leaf(status)));

        _focus = _layout.AsFocusRing(options.Keymap);
    }

    public void Draw() => _layout.Draw(_surface.Content);

    public ViewRoute Handle(ConsoleKeyInfo key) => _focus.Handle(key);

    public ViewRoute HandleMouse(MouseEvent mouse) => _focus.HandleMouse(mouse);

    public IReadOnlyList<ViewCommand> Commands() =>
    [
        ViewCommand.Navigating(ConsoleKey.Escape, static () => "back", static () => ViewKind.Commander),
        new()
        {
            Binding = new(ConsoleKey.F3),
            Label = static () => "stop the search",
            IsEnabled = () => _finder.IsRunning,
            Run = () =>
            {
                _finder.Stop();

                return ViewRoute.None;
            },
        },
    ];

    private void DrawHeader(SurfaceRegion header)
    {
        header.WriteLine(0, _finder.What, Theme.Header);
        header.WriteLine(1, $"{_finder.Found.Count} found in {_finder.Looked} folders", Theme.Muted);

        if (!_finder.IsRunning)
        {
            return;
        }

        _spinner.Advance();
        _spinner.Draw(header.SplitLeft(header.Width - 1).Right);
    }

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

        return folder.Length == 0 ? hit.Entry.Name : $"{folder}  ·  {hit.Entry.Name}";
    }

    private string Said() => _finder.IsRunning
        ? $"{_spinner.Current} searching, F3 stops"
        : _finder.Found.Count == 0 ? "nothing found" : $"{_finder.Found.Count} found";

    /// <summary>
    /// Sends the panel that was active to the folder a result is in, with the cursor on the file
    /// itself. The panel is left to reload on its own when the screen comes back.
    /// </summary>
    /// <param name="hit">The result that was chosen.</param>
    /// <returns>The screen with the panels on it.</returns>
    private ViewRoute Open(Hit hit)
    {
        var panel = _panels.RightIsActive.Value ? _panels.Right : _panels.Left;

        panel.GoTo(hit.Folder);
        panel.Cursor = hit.Entry.Name;

        _panels.Moved();
        _state.Output = hit.Entry.Path;

        return ViewKind.Commander;
    }
}
