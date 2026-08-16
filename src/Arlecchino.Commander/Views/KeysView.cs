using System;
using System.Collections.Generic;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Commands;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Layout;
using Arlecchino.Navigation;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Text;
using Arlecchino.Widgets.Lists;
using static Arlecchino.Layout.PaneSplit;
using static Arlecchino.Layout.PaneTree;

namespace Arlecchino.Commander.Views;

/// <summary>
/// Every key at once: what the framework answers to wherever you are, and what the panels answer to. The
/// framework brings a screen of its own for this, and the route is taken over rather than the framework
/// changed.
/// </summary>
public sealed class KeysView : IArlecchinoView
{
    private const int Column = 34;
    private const int Between = 3;
    private const int Chip = 2;

    private readonly Surface _surface;
    private readonly Navigator _navigator;
    private readonly ArlecchinoKeymap _keymap;
    private readonly ScrollPane _pane;
    private readonly PaneTree _layout;
    private readonly List<Row> _everywhere = [];
    private readonly List<Row> _panels = [];

    private int _room;
    private bool _doubled;

    /// <summary>Builds the screen over the keys there are.</summary>
    /// <param name="surface">What is drawn on.</param>
    /// <param name="keyboard">Where the panels left their table of keys.</param>
    /// <param name="options">The keys and the wording this was started with.</param>
    /// <param name="navigator">How the screen is left.</param>
    public KeysView(Surface surface, Keyboard keyboard, ArlecchinoOptions options, Navigator navigator)
    {
        _surface = surface;
        _navigator = navigator;
        _keymap = options.Keymap;

        Build(keyboard, options);

        _pane = new(options.Keymap)
        {
            IsFocused = true,
            ContentHeight = Height,
            Content = Paint,
        };

        _layout = Branch(
            Rows,
            Sheet.Head,
            Leaf(DrawHeader),
            Branch(Rows, PaneSize.CellsFromEnd(Sheet.Foot), Leaf(_pane), Leaf(DrawFooter)));
    }

    /// <summary>One line of the listing: a key and what it does, or the name of a section.</summary>
    /// <param name="Key">The key, or nothing for a heading.</param>
    /// <param name="Text">What it does, or what the section is called.</param>
    /// <param name="IsHeading">Whether the line names a section.</param>
    private sealed record Row(string Key, string Text, bool IsHeading = false);

    /// <inheritdoc/>
    public void Draw() => _layout.Draw(Sheet.Inside(_surface.Content));

    /// <inheritdoc/>
    public ViewRoute Handle(KeyPress key)
    {
        if (!_keymap.Cancel.Matches(key) && !_keymap.Help.Matches(key))
        {
            return _pane.Handle(key).Route;
        }

        _navigator.Back();

        return ViewRoute.None;
    }

    /// <inheritdoc/>
    public ViewRoute HandleMouse(MouseEvent mouse) => _pane.HandleMouse(mouse).Route;

    /// <inheritdoc/>
    public IReadOnlyList<ViewCommand> Commands() => [];

    private static void DrawHeader(SurfaceRegion header) =>
        Sheet.Title(header, Loc(LocString.MenuKeys), Loc(LocString.KeysSaid));

    private void DrawFooter(SurfaceRegion footer) => Sheet.Hints(
        footer,
        Loc(LocString.KeysCounted, _everywhere.Count + _panels.Count - 2),
        Loc(LocString.KeysHints));

    private int Height() => _doubled
        ? Math.Max(_everywhere.Count, _panels.Count)
        : _everywhere.Count + _panels.Count + 1;

    /// <summary>
    /// Draws the two sections side by side where there is width for both, and one under the other where
    /// there is not.
    /// </summary>
    /// <param name="region">The rows the scrolling pane handed over.</param>
    private void Paint(SurfaceRegion region)
    {
        _doubled = region.Width >= (Column * 2) + Between;
        _room = _doubled ? (region.Width - Between) / 2 : region.Width;

        if (_doubled)
        {
            Write(region, _everywhere, 0, 0);
            Write(region, _panels, 0, _room + Between);

            return;
        }

        Write(region, _everywhere, 0, 0);
        Write(region, _panels, _everywhere.Count + 1, 0);
    }

    /// <summary>Writes one section: its heading, then a chip and a description for every key under it.</summary>
    /// <param name="region">Where to draw.</param>
    /// <param name="rows">The section.</param>
    /// <param name="top">The row it starts on.</param>
    /// <param name="left">The column it starts at.</param>
    private void Write(SurfaceRegion region, List<Row> rows, int top, int left)
    {
        var coat = Skin.Terminal;
        var chips = 0;

        foreach (var row in rows)
        {
            chips = Math.Max(chips, TextWidth.Of(row.Key) + Chip);
        }

        var room = _room - chips - Chip;

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var at = top + index;

            if (row.IsHeading)
            {
                region.Write(at, left, TextWidth.Truncate(row.Text.ToUpperInvariant(), _room), coat.Label);

                continue;
            }

            region.Write(at, left, TextWidth.PadRight($" {row.Key} ", chips), Skin.Paint(Skin.Sea, Skin.Chip));

            if (room > 0)
            {
                region.Write(at, left + chips + Chip, TextWidth.Truncate(row.Text, room), coat.Second);
            }
        }
    }

    /// <summary>
    /// Reads both tables once, since neither of them changes while the screen is open: what the framework
    /// answers to everywhere, and what the panels bound for themselves.
    /// </summary>
    /// <param name="keyboard">Where the panels left their table.</param>
    /// <param name="options">Supplies the keymap those keys are named by.</param>
    private void Build(Keyboard keyboard, ArlecchinoOptions options)
    {
        _everywhere.Add(new("", Loc(LocString.KeysEverywhere), true));

        foreach (var (binding, action) in options.Strings.HelpKeys(options.Keymap))
        {
            _everywhere.Add(new(binding.ToString(), action));
        }

        _panels.Add(new("", Loc(LocString.KeysPanels), true));

        foreach (var command in keyboard.Panels)
        {
            _panels.Add(new(command.Binding.ToString(), command.Label()));
        }
    }
}
