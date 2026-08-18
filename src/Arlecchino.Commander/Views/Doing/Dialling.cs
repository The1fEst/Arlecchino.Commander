using System;
using System.Collections.Generic;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Files.Ssh;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Commander.Widgets.Dialogs;
using Arlecchino.State;

namespace Arlecchino.Commander.Views.Doing;

/// <summary>
/// Getting a panel onto a server and off it again. A connection lands in the panel that asked for it
/// rather than in a tab of its own.
/// </summary>
public sealed class Dialling
{
    private readonly Dialogs _dialogs;
    private readonly Remote _remote;
    private readonly ArlecchinoState _state;

    /// <summary>Sets connecting up.</summary>
    /// <param name="dialogs">How anything is asked.</param>
    /// <param name="remote">Where the connection that was made is remembered.</param>
    /// <param name="state">Where the last word said is kept.</param>
    public Dialling(Dialogs dialogs, Remote remote, ArlecchinoState state)
    {
        _dialogs = dialogs;
        _remote = remote;
        _state = state;
    }

    /// <summary>Opens the hosts the SSH config already knows about.</summary>
    /// <param name="panel">The panel that would be connected.</param>
    public void OpenSavedHosts(FilePanel panel)
    {
        var hosts = SshConfig.Hosts();

        if (hosts.Count == 0)
        {
            _state.Output = Loc(LocString.SaidNoHosts, SshConfig.Location);

            return;
        }

        var items = new List<string>(hosts.Count);

        foreach (var host in hosts)
        {
            items.Add(host.Describe());
        }

        _dialogs.Pick(Loc(LocString.PickSavedHosts),
            items,
            chosen =>
            {
                for (var index = 0; index < items.Count; index++)
                {
                    if (items[index] != chosen)
                    {
                        continue;
                    }

                    Dial(panel, hosts[index], hosts[index].AsConnection(""));

                    return;
                }
            });
    }

    /// <summary>Puts a panel back on the disk.</summary>
    /// <param name="panel">The panel.</param>
    public void Disconnect(FilePanel panel)
    {
        if (!panel.Source.IsRemote)
        {
            _state.Output = Loc(LocString.SaidPanelIsLocal);

            return;
        }

        var label = panel.Source.Label;

        panel.Connect(new LocalSource(), Environment.CurrentDirectory);

        _state.Output = Loc(LocString.SaidDisconnected, label);
    }

    /// <summary>Opens a connection, asking for a password only if the server turns out to want one.</summary>
    /// <param name="panel">Where it lands.</param>
    /// <param name="host">The host as the config describes it.</param>
    /// <param name="connection">What is being connected with.</param>
    private void Dial(FilePanel panel, SshHost host, Connection connection)
    {
        _state.Output = Loc(LocString.SaidConnecting, host.Alias);

        Connector.Start(
            connection,
            (source, folder) =>
            {
                _remote.Ssh = connection;
                panel.Connect(source, folder);
                _state.Output = Loc(LocString.Joined, host.Alias, folder);
            },
            (message, denied) => AskPassword(panel, host, connection, message, denied));
    }

    /// <summary>
    /// Asks for the password the server asked for. A refusal with a password already tried is not asked
    /// again, since that is a wrong password rather than a missing one.
    /// </summary>
    /// <param name="panel">Where the connection would land.</param>
    /// <param name="host">The host.</param>
    /// <param name="connection">What was being connected with.</param>
    /// <param name="message">What the server said.</param>
    /// <param name="denied">Whether it was a refusal rather than a failure.</param>
    private void AskPassword(FilePanel panel, SshHost host, Connection connection, string message, bool denied)
    {
        if (!denied || connection.Password.Length > 0)
        {
            _dialogs.Say(Loc(LocString.SaidCouldNotOpen, host.Alias), message);

            return;
        }

        _state.Output = message;

        _dialogs.AskFor(
            Loc(LocString.PasswordTitle, host.User, host.HostName),
            Loc(LocString.OperationPassword),
            "",
            Loc(LocString.ConnectVerb),
            password => Dial(panel, host, connection with { Password = password }),
            secret: true);
    }
}
