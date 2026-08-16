using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Ssh;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Commander.Widgets.Dialogs;
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
using Renci.SshNet;
using Renci.SshNet.Common;
using static Arlecchino.Layout.PaneSplit;
using static Arlecchino.Layout.PaneTree;

namespace Arlecchino.Commander.Views;

public sealed class SshView : IArlecchinoView
{
    private readonly Surface _surface;
    private readonly Remote _session;
    private readonly ArlecchinoState _state;
    private readonly Spinner _spinner = new();
    private readonly List<string> _lines = [];
    private readonly PaneTree _layout;
    private readonly FocusRing _focus;

    private string _command = "";
    private bool _running;

    public SshView(Surface surface, Remote session, ArlecchinoState state, ArlecchinoOptions options)
    {
        _surface = surface;
        _session = session;
        _state = state;

        var output = new ScrollPane(options.Keymap)
        {
            ContentHeight = () => _lines.Count,
            Content = region =>
            {
                for (var row = 0; row < _lines.Count; row++)
                {
                    region.WriteLine(row, _lines[row], Style(_lines[row]));
                }
            },
        };

        _layout = Branch(
            Rows,
            Sheet.Head,
            Leaf(DrawHeader),
            Branch(Rows, PaneSize.CellsFromEnd(Sheet.Foot), Leaf(output), Leaf(DrawFooter)));

        _focus = _layout.AsFocusRing(options.Keymap);
    }

    public void Draw() => _layout.Draw(Sheet.Inside(_surface.Content));

    public ViewRoute Handle(KeyPress key) => _focus.Handle(key);

    public ViewRoute HandleMouse(MouseEvent mouse) => _focus.HandleMouse(mouse);

    public IReadOnlyList<ViewCommand> Commands() =>
    [
        Bind.Going(new(ConsoleKey.Escape), LocString.KeyBack, static () => ViewKind.Commander),
        Bind.To(new(ConsoleKey.Enter), LocString.SshRun, Ask),
        Bind.To(new(ConsoleKey.K, KeyModifiers.Control), LocString.KeyClear, _lines.Clear),
    ];

    private void DrawHeader(SurfaceRegion header)
    {
        Sheet.Title(
            header,
            _session.Ssh is { } ssh ? Loc(LocString.SshTitle, ssh.Label) : Loc(LocString.SshNowhere),
            _session.Ssh is null
                ? Loc(LocString.SshCredentials)
                : Loc(LocString.SshLast, _command.Length == 0 ? Loc(LocString.SshNoneYet) : _command));

        if (!_running)
        {
            return;
        }

        _spinner.Advance();
        _spinner.Draw(header.SplitLeft(header.Width - 1).Right);
    }

    private void DrawFooter(SurfaceRegion footer) => Sheet.Hints(footer, Said(), Loc(LocString.SshHints));

    private string Said() => _running
        ? Loc(LocString.SshRunning, _spinner.Current)
        : _lines.Count == 0
            ? Loc(LocString.OutputNothing)
            : Loc(LocString.OutputLines, _lines.Count);

    private void Ask()
    {
        if (_running)
        {
            _state.Output = Loc(LocString.SaidStillRunning);
            return;
        }

        if (_session.Ssh is not { } ssh)
        {
            _state.Output = Loc(LocString.SshConnectFirst);
            return;
        }

        _state.Modal = new OperationModal(
            new()
            {
                Title = Loc(LocString.SshRunOn, ssh.Host),
                Key = "",
                Verb = Loc(LocString.SshVerb),
                Weight = Weight.Moves,
                FieldLabel = Loc(LocString.SshCommand),
                Value = _command,
                FieldHint = Loc(LocString.SshWhere),
                Confirm = asking => Run(ssh, asking.Value.Trim()),
            });
    }

    private void Run(Connection ssh, string command)
    {
        _command = command;
        _running = true;

        _lines.Add($"$ {command}");

        FrameThread.Post(async () =>
        {
            var report = await Task.Run(() => Execute(ssh, command));

            _running = false;
            _lines.AddRange(report);
            _state.Invalidate();
        });
    }

    private static List<string> Execute(Connection ssh, string command)
    {
        try
        {
            using var client = new SshClient(Credentials.For(ssh));
            var check = Credentials.Watch(client, ssh);

            try
            {
                client.Connect();
            }
            catch (SshException) when (check.Refusal.Length > 0)
            {
                return [check.Refusal];
            }

            using var running = client.RunCommand(command);
            var lines = new List<string>();

            lines.AddRange(Split(running.Result));
            lines.AddRange(Split(running.Error));
            lines.Add($"[exit {running.ExitStatus}]");

            return lines;
        }
        catch (Exception error) when (error is SshException or SocketException or ObjectDisposedException)
        {
            return [$"[failed] {error.Message}"];
        }
    }

    private static string[] Split(string text) => text.Length == 0
        ? []
        : text.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');

    private static TermColor Style(string line)
    {
        if (line.StartsWith("$ ", StringComparison.Ordinal))
        {
            return Skin.Terminal.Accent;
        }

        if (line.StartsWith("[failed]", StringComparison.Ordinal))
        {
            return Skin.Terminal.Warning;
        }

        return line.StartsWith("[exit ", StringComparison.Ordinal) ? Skin.Terminal.Meta : Skin.Terminal.Text;
    }
}
