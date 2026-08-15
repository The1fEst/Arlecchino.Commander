using System;
using System.Collections.Generic;
using System.IO;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Files.Ssh;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Commander.Widgets.Dialogs;
using Arlecchino.Commands;
using Arlecchino.Focus;
using Arlecchino.Forms;
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

public sealed class ConnectView : IArlecchinoView, IDisposable
{
    private const int LowestPort = 1;
    private const int HighestPort = 65535;

    private static readonly string[] Schemes = ["sftp", "ftp"];
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

        var form = new Form(state, options)
        {
            Fields =
            [
                Field.Choice(static () => Loc(LocString.ConnectSaved),
                    Aliases(saved),
                    session.Saved,
                    static () => Loc(LocString.ConnectSavedHint)),
                Field.Choice(static () => Loc(LocString.ConnectProtocol),
                    Schemes,
                    session.Scheme,
                    static () => Loc(LocString.ConnectProtocolHint)),
                Field.Text(static () => Loc(LocString.ConnectHost),
                    session.Host,
                    Filled,
                    static () => Loc(LocString.ConnectHostHint)),
                Field.Number(static () => Loc(LocString.ConnectPort),
                    session.Port,
                    LowestPort,
                    HighestPort,
                    static () => Loc(LocString.ConnectPortHint)),
                Field.Text(static () => Loc(LocString.ConnectUser),
                    session.User,
                    Filled),
                Field.Secret(static () => Loc(LocString.ConnectPassword),
                    session.Password,
                    static () => Loc(LocString.ConnectPasswordHint)),
                Field.PathFrom(static () => Loc(LocString.ConnectKeyFile),
                    session.KeyFile,
                    ViewKind.Connect,
                    false,
                    Keys,
                    static () => Loc(LocString.ConnectKeyFileHint)),
                Field.Text(static () => Loc(LocString.ConnectFolder),
                    session.Folder,
                    null,
                    static () => Loc(LocString.ConnectFolderHint)),
                Field.Action(static () => Loc(LocString.ConnectVerb),
                    Start,
                    () => !session.Connecting.Value && Ready(session))
            ]
        };

        _layout = Branch(
            Rows,
            Sheet.Head,
            Leaf(DrawHeader),
            Branch(Rows, PaneSize.CellsFromEnd(Sheet.Foot), Leaf(form), Leaf(DrawFooter)));

        _focus = _layout.AsFocusRing(options.Keymap);
        _watchingSaved = session.Saved.Subscribe(() => Fill(saved, session));
        _watchingScheme = session.Scheme.Subscribe(() =>
            session.Port.Value = Connection.PortFor(session.Scheme.Value == "ftp" ? Protocol.Ftp : Protocol.Sftp));
    }

    public void Draw()
    {
        _layout.Draw(_surface.Content);
    }

    public ViewRoute Handle(KeyPress key)
    {
        return _focus.Handle(key);
    }

    public ViewRoute HandleMouse(MouseEvent mouse)
    {
        return _focus.HandleMouse(mouse);
    }

    public IReadOnlyList<ViewCommand> Commands()
    {
        return
        [
            Bind.Going(new(ConsoleKey.Escape), LocString.KeyBack, static () => ViewKind.Commander)
        ];
    }

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

    private ViewRoute Start()
    {
        var wanted = _session.Wanted();

        _session.Connecting.Value = true;
        _session.Failure.Value = "";

        Connector.Start(wanted, (source, folder) => Landed(wanted, source, folder), Failed);

        return ViewRoute.None;
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
                Verb = Loc(LocString.ConnectClose),
                Weight = Weight.Destroys,
                Note = _ => new(message, true),
                Confirm = static _ => { }
            });
    }

    private PanelState Side()
    {
        return _sessions.RightIsActive.Value ? _sessions.Right : _sessions.Left;
    }

    private static string? Filled(string text)
    {
        return text.Trim().Length == 0 ? Loc(LocString.ConnectNeeded) : null;
    }

    private static string Keys()
    {
        return Path.Combine(Listing.Home(), ".ssh");
    }

    private static List<string> Aliases(IReadOnlyList<SshHost> saved)
    {
        var names = new List<string>(saved.Count + 1) { "" };

        foreach (var host in saved)
        {
            names.Add(host.Alias);
        }

        return names;
    }

    private static void Fill(IReadOnlyList<SshHost> saved, Remote session)
    {
        foreach (var host in saved)
        {
            if (host.Alias != session.Saved.Value)
            {
                continue;
            }

            session.Fill(host);
            return;
        }
    }

    private static bool Ready(Remote session)
    {
        return session.Host.Value.Trim().Length > 0 && session.User.Value.Trim().Length > 0;
    }
}
