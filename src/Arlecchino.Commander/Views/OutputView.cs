using System;
using System.Collections.Generic;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Commands;
using Arlecchino.Focus;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Layout;
using Arlecchino.Navigation;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Colors;
using Arlecchino.Widgets.Lists;
using static Arlecchino.Layout.PaneSplit;
using static Arlecchino.Layout.PaneTree;

namespace Arlecchino.Commander.Views;

/// <summary>
/// What the commands typed on the command line have printed, kept in one roll the way a shell would
/// leave it on the screen behind the panels.
/// </summary>
public sealed class OutputView : IArlecchinoView
{
    private readonly Surface _surface;
    private readonly Runner _runner;
    private readonly PaneTree _layout;
    private readonly FocusRing _focus;

    public OutputView(Surface surface, Runner runner, ArlecchinoOptions options)
    {
        _surface = surface;
        _runner = runner;

        var roll = new ScrollPane(options.Keymap)
        {
            ContentHeight = () => _runner.Lines.Count,
            Content = region =>
            {
                for (var row = 0; row < _runner.Lines.Count; row++)
                {
                    region.WriteLine(row, _runner.Lines[row], Style(_runner.Lines[row]));
                }
            },
        };

        _layout = Branch(
            Rows,
            Sheet.Head,
            Leaf(DrawHeader),
            Branch(Rows, PaneSize.CellsFromEnd(Sheet.Foot), Leaf(roll), Leaf(DrawFooter)));

        _focus = _layout.AsFocusRing(options.Keymap);
    }

    public void Draw() => _layout.Draw(_surface.Content);

    public ViewRoute Handle(KeyPress key) => _focus.Handle(key);

    public ViewRoute HandleMouse(MouseEvent mouse) => _focus.HandleMouse(mouse);

    public IReadOnlyList<ViewCommand> Commands() =>
    [
        Bind.Going(new(ConsoleKey.Escape), LocString.KeyBack, static () => ViewKind.Commander),
        Bind.To(new(ConsoleKey.K, KeyModifiers.Control), LocString.KeyClear, _runner.Clear),
    ];

    private void DrawHeader(SurfaceRegion header) =>
        Sheet.Title(header, Loc(LocString.OutputTitle), Loc(LocString.OutputSaid));

    private void DrawFooter(SurfaceRegion footer) => Sheet.Hints(footer, Said(), Loc(LocString.OutputHints));

    private string Said()
    {
        if (_runner.IsRunning)
        {
            return Loc(LocString.OutputRunning, _runner.Last);
        }

        return _runner.Lines.Count == 0
            ? Loc(LocString.OutputNothing)
            : Loc(LocString.OutputLines, _runner.Lines.Count);
    }

    /// <summary>
    /// What a line of output is written in: the command itself in the accent, what a shell said in
    /// plain text, and the two lines the runner writes itself quieter than either.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The style.</returns>
    private static TermColor Style(string line)
    {
        var coat = Skin.Terminal;

        if (line.StartsWith("$ ", StringComparison.Ordinal))
        {
            return coat.Accent;
        }

        if (line.StartsWith("[failed]", StringComparison.Ordinal))
        {
            return coat.Warning;
        }

        return line.StartsWith("[exit ", StringComparison.Ordinal) ? coat.Meta : coat.Text;
    }
}
