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
using Arlecchino.Widgets;
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

        var status = new StatusBar
        {
            Left = [Said],
            Right = [static () => "Ctrl+K clears", static () => "Esc back"],
        };

        _layout = Branch(Rows, PaneSize.CellsFromEnd(1), Leaf(roll, static () => "Output"), Leaf(status));
        _focus = _layout.AsFocusRing(options.Keymap);
    }

    public void Draw() => _layout.Draw(_surface.Content);

    public ViewRoute Handle(ConsoleKeyInfo key) => _focus.Handle(key);

    public ViewRoute HandleMouse(MouseEvent mouse) => _focus.HandleMouse(mouse);

    public IReadOnlyList<ViewCommand> Commands() =>
    [
        ViewCommand.Navigating(ConsoleKey.Escape, static () => "back", static () => ViewKind.Commander),
        ViewCommand.For(new KeyBinding(ConsoleKey.K, ConsoleModifiers.Control), static () => "clear", _runner.Clear),
    ];

    private string Said()
    {
        if (_runner.IsRunning)
        {
            return $"running {_runner.Last}";
        }

        return _runner.Lines.Count == 0 ? "nothing run yet" : $"{_runner.Lines.Count} lines";
    }

    private static TermColor Style(string line)
    {
        if (line.StartsWith("$ ", StringComparison.Ordinal))
        {
            return Theme.Accent;
        }

        if (line.StartsWith("[failed]", StringComparison.Ordinal))
        {
            return Theme.Error;
        }

        return line.StartsWith("[exit ", StringComparison.Ordinal) ? Theme.Muted : Theme.Default;
    }
}
