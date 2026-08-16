using System;
using System.Collections.Generic;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Files.Ssh;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Views.Connecting;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Commander.Widgets.Dialogs;
using Arlecchino.Commands;
using Arlecchino.Focus;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Layout;
using Arlecchino.Navigation;
using Arlecchino.Rendering;
using Arlecchino.State;
using Arlecchino.Widgets.Readouts;
using static Arlecchino.Layout.PaneSplit;
using static Arlecchino.Layout.PaneTree;

namespace Arlecchino.Commander.Views;

/// <summary>
/// Where a panel is pointed at a server. What it asks for is <see cref="ConnectFields"/>; this is the
/// frame around those rows, and what happens once the button is pressed.
/// </summary>
public sealed class ConnectView : IArlecchinoView, IDisposable
{
    private readonly FocusRing _focus;
    private readonly PaneTree _layout;
    private readonly Navigator _navigation;
    private readonly Remote _session;
    private readonly Sessions _sessions;
    private readonly Spinner _spinner = new();
    private readonly ArlecchinoState _state;

    private readonly Surface _surface;
    private readonly IDisposable _watchingSaved;
    private readonly IDisposable _watchingScheme;

    /// <summary>Builds the screen over whatever was last typed into it.</summary>
    /// <param name="surface">What is drawn on.</param>
    /// <param name="session">Where the answers and the connection that was made are kept.</param>
    /// <param name="sessions">Says which panel is being connected.</param>
    /// <param name="state">Where the dialog on top and the last word said live.</param>
    /// <param name="options">The keys this was started with.</param>
    /// <param name="navigation">How the screen is left once a server answers.</param>
    public ConnectView(
        Surface surface,
        Remote session,
        Sessions sessions,
        ArlecchinoState state,
        ArlecchinoOptions options,
        Navigator navigation)
    {
        _surface = surface;
        _session = session;
        _sessions = sessions;
        _state = state;
        _navigation = navigation;

        var saved = SshConfig.Hosts();
        var form = ConnectFields.For(session, new(state), options.Keymap, saved, Start);

        _layout = Branch(
            Rows,
            Sheet.Head,
            Leaf(DrawHeader),
            Branch(Rows, PaneSize.CellsFromEnd(Sheet.Foot), Leaf(form), Leaf(DrawFooter)));

        _focus = _layout.AsFocusRing(options.Keymap);
        _watchingSaved = session.Saved.Subscribe(() => ConnectFields.Fill(saved, session));
        _watchingScheme = session.Scheme.Subscribe(() =>
            session.Port.Value = Connection.PortFor(session.Scheme.Value == "ftp" ? Protocol.Ftp : Protocol.Sftp));
    }

    /// <inheritdoc/>
    public void Draw()
    {
        _layout.Draw(Sheet.Inside(_surface.Content));
    }

    /// <inheritdoc/>
    public ViewRoute Handle(KeyPress key)
    {
        return _focus.Handle(key);
    }

    /// <inheritdoc/>
    public ViewRoute HandleMouse(MouseEvent mouse)
    {
        return _focus.HandleMouse(mouse);
    }

    /// <inheritdoc/>
    public IReadOnlyList<ViewCommand> Commands()
    {
        return
        [
            Bind.Going(new(ConsoleKey.Escape), LocString.KeyBack, static () => ViewKind.Commander)
        ];
    }

    /// <summary>Gives up the watches on the saved host and the protocol, which the screen outlived.</summary>
    public void Dispose()
    {
        _watchingScheme.Dispose();
        _watchingSaved.Dispose();
    }

    private void DrawHeader(SurfaceRegion header)
    {
        var side = Loc(_sessions.RightIsActive.Value ? LocString.ConnectRight : LocString.ConnectLeft);

        Sheet.Title(header, Loc(LocString.ConnectTitle, side), Loc(LocString.ConnectSide));

        if (!_session.Connecting.Value)
        {
            return;
        }

        _spinner.Advance();
        _spinner.Draw(header.SplitLeft(header.Width - 1).Right);
    }

    private void DrawFooter(SurfaceRegion footer)
    {
        Sheet.Hints(footer, Said(), Loc(LocString.ConnectHints));
    }

    private string Said()
    {
        if (_session.Connecting.Value)
        {
            return Loc(LocString.ConnectBusy, _spinner.Current, _session.Host.Value);
        }

        return _session.Failure.Value.Length > 0 ? _session.Failure.Value : Loc(LocString.ConnectNothing);
    }

    private void Start()
    {
        var wanted = _session.Wanted();

        _session.Connecting.Value = true;
        _session.Failure.Value = "";

        Connector.Start(wanted, (source, folder) => Landed(wanted, source, folder), Failed);
    }

    private void Landed(Connection connection, IFileSource source, string folder)
    {
        _session.Connecting.Value = false;

        if (connection.Protocol == Protocol.Sftp)
        {
            _session.Ssh = connection;
        }

        Side().Connect(source, folder);

        _state.Output = Loc(LocString.ConnectLanded, connection.Label);
        _navigation.Apply(ViewKind.Commander);
    }

    private void Failed(string message, bool denied)
    {
        _session.Connecting.Value = false;
        _session.Failure.Value = message;

        _state.Modal = new OperationModal(
            new()
            {
                Title = Loc(denied ? LocString.ConnectRefused : LocString.ConnectFailed),
                Key = "",
                Verb = Loc(LocString.CloseVerb),
                Weight = Weight.Destroys,
                Note = _ => new(message, true),
                Confirm = static _ => { }
            });
    }

    private PanelState Side()
    {
        return _sessions.RightIsActive.Value ? _sessions.Right : _sessions.Left;
    }
}
