using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Arlecchino.Atoms;
using Arlecchino.Commander.Files.Ssh;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Dialogs;
using Arlecchino.Commander.Widgets.Forms;
using Arlecchino.Hosting;

namespace Arlecchino.Commander.Views.Connecting;

/// <summary>
/// What the connect screen asks for, as rows of a form. Every one of them is answered through the dialogs
/// the rest of the application asks through: a line of text, or a list to pick out of.
/// </summary>
public static class ConnectFields
{
    private const int LowestPort = 1;
    private const int HighestPort = 65535;

    private static readonly string[] Schemes = ["sftp", "ftp"];

    /// <summary>Builds the form.</summary>
    /// <param name="session">Where every answer is kept.</param>
    /// <param name="dialogs">How each of them is asked.</param>
    /// <param name="keymap">Keys the rows are walked by.</param>
    /// <param name="saved">The hosts <c>~/.ssh/config</c> names, which the first row picks from.</param>
    /// <param name="connect">What the button at the foot does.</param>
    /// <returns>The rows.</returns>
    public static FormRows For(
        Remote session,
        Dialogs dialogs,
        ArlecchinoKeymap keymap,
        IReadOnlyList<SshHost> saved,
        Action connect) => new(keymap)
    {
        Rows =
        [
            Chosen(LocString.ConnectSaved, LocString.ConnectSavedHint, session.Saved, dialogs, () => Aliases(saved)),
            Chosen(LocString.ConnectProtocol, LocString.ConnectProtocolHint, session.Scheme, dialogs, static () => Schemes),
            Typed(LocString.ConnectHost, LocString.ConnectHostHint, session.Host, dialogs),
            Port(session, dialogs),
            Typed(LocString.ConnectUser, LocString.ConnectUserHint, session.User, dialogs),
            Secret(session, dialogs),
            Key(session, dialogs),
            Typed(LocString.ConnectFolder, LocString.ConnectFolderHint, session.Folder, dialogs),
            new FormButton(
                static () => Loc(LocString.ConnectVerb),
                static () => Loc(LocString.ConnectReady),
                connect,
                () => !session.Connecting.Value && Ready(session)),
        ],
    };

    /// <summary>The form holds enough to try a server once it names a host and an account.</summary>
    /// <param name="session">The answers as they stand.</param>
    /// <returns><c>true</c> when the button may be pressed.</returns>
    public static bool Ready(Remote session) =>
        session.Host.Value.Trim().Length > 0 && session.User.Value.Trim().Length > 0;

    /// <summary>Fills in the rest of the form from the host that was picked out of the saved ones.</summary>
    /// <param name="saved">Every host <c>~/.ssh/config</c> names.</param>
    /// <param name="session">Where the answers are kept.</param>
    public static void Fill(IReadOnlyList<SshHost> saved, Remote session)
    {
        foreach (var host in saved)
        {
            if (host.Alias == session.Saved.Value)
            {
                session.Fill(host);

                return;
            }
        }
    }

    private static FormField Typed(LocString label, LocString hint, Atom<string> value, Dialogs dialogs) =>
        new(
            () => Loc(label),
            () => value.Value,
            () => Loc(hint),
            () => dialogs.AskFor(
                Loc(label),
                Loc(label),
                value.Value,
                Loc(LocString.ConnectKeep),
                typed => value.Value = typed.Trim(),
                Loc(hint)),
            () => value.Value = "");

    private static FormField Secret(Remote session, Dialogs dialogs) =>
        new(
            static () => Loc(LocString.ConnectPassword),
            () => new('•', session.Password.Value.Length),
            static () => Loc(LocString.ConnectPasswordHint),
            () => dialogs.AskFor(
                Loc(LocString.ConnectPassword),
                Loc(LocString.ConnectPassword),
                session.Password.Value,
                Loc(LocString.ConnectKeep),
                typed => session.Password.Value = typed,
                Loc(LocString.ConnectPasswordHint),
                secret: true),
            () => session.Password.Value = "");

    /// <summary>
    /// The port, which is a line of text like the rest and simply refuses an answer that is not a port.
    /// Emptying it puts back the port the protocol is spoken on.
    /// </summary>
    /// <param name="session">Where the answer is kept.</param>
    /// <param name="dialogs">How it is asked.</param>
    /// <returns>The row.</returns>
    private static FormField Port(Remote session, Dialogs dialogs) =>
        new(
            static () => Loc(LocString.ConnectPort),
            () => ((int)session.Port.Value).ToString(CultureInfo.InvariantCulture),
            static () => Loc(LocString.ConnectPortWanted),
            () => dialogs.AskFor(
                Loc(LocString.ConnectPort),
                Loc(LocString.ConnectPort),
                ((int)session.Port.Value).ToString(CultureInfo.InvariantCulture),
                Loc(LocString.ConnectKeep),
                typed => Numbered(session, typed),
                Loc(LocString.ConnectPortHint)),
            () => session.Port.Value = Connection.PortFor(Wanted(session)));

    /// <summary>
    /// The key file, picked out of what is in <c>~/.ssh</c>, since that is where one lives. The last row
    /// of the list is for a key that lives anywhere else, which is then typed out.
    /// </summary>
    /// <param name="session">Where the answer is kept.</param>
    /// <param name="dialogs">How it is asked.</param>
    /// <returns>The row.</returns>
    private static FormField Key(Remote session, Dialogs dialogs) =>
        new(
            static () => Loc(LocString.ConnectKeyFile),
            () => session.KeyFile.Value,
            static () => Loc(LocString.ConnectKeyFileHint),
            () => dialogs.Pick(Loc(LocString.ConnectKeyFile), Keys(), picked => Picked(session, dialogs, picked)),
            () => session.KeyFile.Value = "");

    private static FormField Chosen(
        LocString label,
        LocString hint,
        Atom<string> value,
        Dialogs dialogs,
        Func<IReadOnlyList<string>> options) =>
        new(
            () => Loc(label),
            () => value.Value,
            () => Loc(hint),
            () => dialogs.Pick(Loc(label), options(), picked => value.Value = picked),
            () => value.Value = "");

    private static void Numbered(Remote session, string typed)
    {
        if (int.TryParse(typed.Trim(), CultureInfo.InvariantCulture, out var port) &&
            port is >= LowestPort and <= HighestPort)
        {
            session.Port.Value = port;
        }
    }

    private static void Picked(Remote session, Dialogs dialogs, string picked)
    {
        if (picked != Loc(LocString.ConnectKeyOther))
        {
            session.KeyFile.Value = Path.Combine(Folder(), picked);

            return;
        }

        dialogs.AskFor(
            Loc(LocString.ConnectKeyFile),
            Loc(LocString.ConnectKeyFile),
            session.KeyFile.Value,
            Loc(LocString.ConnectKeep),
            typed => session.KeyFile.Value = typed.Trim(),
            Loc(LocString.ConnectKeyFileHint));
    }

    /// <summary>
    /// Every private key in <c>~/.ssh</c>: what is left once the public halves and the files OpenSSH keeps
    /// there for itself are dropped.
    /// </summary>
    /// <returns>The names, with the row for a key kept elsewhere at the end.</returns>
    private static List<string> Keys()
    {
        var keys = new List<string>();
        var folder = Folder();

        if (Directory.Exists(folder))
        {
            foreach (var path in Directory.GetFiles(folder))
            {
                var name = Path.GetFileName(path);

                if (!name.EndsWith(".pub", StringComparison.OrdinalIgnoreCase) && !Kept(name))
                {
                    keys.Add(name);
                }
            }
        }

        keys.Sort(StringComparer.OrdinalIgnoreCase);
        keys.Add(Loc(LocString.ConnectKeyOther));

        return keys;
    }

    private static bool Kept(string name) =>
        name is "config" or "known_hosts" or "known_hosts.old" or "authorized_keys" || name.StartsWith('.');

    private static string Folder() => Path.Combine(Listing.Home(), ".ssh");

    private static Protocol Wanted(Remote session) =>
        session.Scheme.Value == "ftp" ? Protocol.Ftp : Protocol.Sftp;

    private static List<string> Aliases(IReadOnlyList<SshHost> saved)
    {
        var names = new List<string>(saved.Count);

        foreach (var host in saved)
        {
            names.Add(host.Alias);
        }

        return names;
    }
}
