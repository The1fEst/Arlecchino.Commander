using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Arlecchino.Commander.Model;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Arlecchino.Commander.Files;

public sealed class SftpSource : IFileSource
{
    private const int Sessions = 8;

    private readonly SftpPool _pool;
    private readonly Connection _connection;
    private readonly Lock _shellGate = new();

    private SshClient? _session;
    private bool _shellRefused;
    private Shell? _dialect;

    private SftpSource(Connection connection, SftpPool pool)
    {
        _connection = connection;
        _pool = pool;

        Label = connection.Label;
    }

    public string Label { get; }

    public bool IsRemote => true;

    public int Concurrency => Sessions;

    public string Home
    {
        get
        {
            using var lease = _pool.Take();
            var client = lease.Client;

            return Guarded(() => client.WorkingDirectory);
        }
    }

    public static SftpSource Connect(Connection connection) => new(connection, new(connection, Sessions));

    public IShellRun Start(string command, string folder) => new RemoteRun(this, command, folder);

    public bool WalksCheaply => false;

    /// <summary>
    /// Always, since a server has one tree and a move within it never crosses a volume the way two
    /// drives on a disk do.
    /// </summary>
    /// <param name="from">Where it is now.</param>
    /// <param name="target">Where it is going.</param>
    /// <returns><c>true</c>, always.</returns>
    public bool SameVolume(string from, string target) => true;

    public string Combine(string folder, string name) => RemotePaths.Combine(folder, name);

    public string? Parent(string folder) => RemotePaths.Parent(folder);

    public string NameOf(string path) => RemotePaths.NameOf(path);

    public bool FolderExists(string folder)
    {
        using var lease = _pool.Take();
        var client = lease.Client;

        try
        {
            return client.Exists(folder) && client.GetAttributes(folder).IsDirectory;
        }
        catch (Exception error) when (error is SshException or ObjectDisposedException)
        {
            return false;
        }
    }

    public string Free(string folder) => "";

    public string Mode(FileEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        using var lease = _pool.Take();
        var client = lease.Client;

        try
        {
            return Modes.Write(RemotePaths.ModeOf(client.GetAttributes(entry.Path)));
        }
        catch (Exception error) when (error is SshException or ObjectDisposedException)
        {
            return "";
        }
    }

    public bool TryChangeMode(FileEntry entry, string mode)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (Modes.AsDigits(mode) is not { } wanted)
        {
            return false;
        }

        using var lease = _pool.Take();
        var client = lease.Client;

        try
        {
            client.ChangePermissions(entry.Path, (short)wanted);

            return true;
        }
        catch (Exception error) when (error is SshException or ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Makes a link on the server. SFTP has a request of its own for a symbolic link; a hard link it
    /// has none for, so that one is asked of the shell.
    /// </summary>
    /// <param name="path">Where the link goes.</param>
    /// <param name="target">What it points at.</param>
    /// <param name="hard">Whether it is a hard link.</param>
    /// <returns><c>false</c> when the server refused it.</returns>
    public bool TryLink(string path, string target, bool hard)
    {
        if (hard)
        {
            return Linking(path, target) is { } command &&
                Run(command, RemotePaths.Parent(path) ?? RemotePaths.Root) is { Status: 0 };
        }

        using var lease = _pool.Take();
        var client = lease.Client;

        try
        {
            client.SymbolicLink(target, path);

            return true;
        }
        catch (Exception error) when (error is SshException or ObjectDisposedException)
        {
            return false;
        }
    }

    public IReadOnlyList<FileEntry> List(string folder, bool showHidden)
    {
        using var lease = _pool.Take();
        var client = lease.Client;

        return Guarded(() =>
        {
            var entries = new List<FileEntry>();

            if (RemotePaths.Parent(folder) is { } parent)
            {
                entries.Add(new("..", parent, true, true, 0, default, false, false));
            }

            foreach (var found in client.ListDirectory(folder))
            {
                if (found.Name is "." or "..")
                {
                    continue;
                }

                var hidden = RemotePaths.IsHidden(found.Name);

                if (hidden && !showHidden)
                {
                    continue;
                }

                entries.Add(new(
                    found.Name,
                    found.FullName,
                    found.IsDirectory,
                    false,
                    found.IsDirectory ? 0 : found.Length,
                    found.LastWriteTime,
                    hidden,
                    false));
            }

            return (IReadOnlyList<FileEntry>)entries;
        });
    }

    public Stream OpenRead(string path)
    {
        var lease = _pool.Take();
        var client = lease.Client;

        return Leased(lease, () => client.OpenRead(path));
    }

    public Stream Create(string path)
    {
        var lease = _pool.Take();
        var client = lease.Client;

        return Leased(lease, () => client.Create(path));
    }

    public void CreateFolder(string path)
    {
        using var lease = _pool.Take();
        var client = lease.Client;

        Guarded(() =>
        {
            if (!client.Exists(path))
            {
                client.CreateDirectory(path);
            }
        });
    }

    public void Delete(FileEntry entry)
    {
        using var lease = _pool.Take();
        var client = lease.Client;

        Guarded(() =>
        {
            if (entry.IsFolder)
            {
                client.DeleteDirectory(entry.Path);
                return;
            }

            client.DeleteFile(entry.Path);
        });
    }

    /// <summary>
    /// Removes the tree with one command over SSH rather than a request per file. A server on
    /// the other side of the world answers a delete in a fraction of a second, which a thousand times
    /// over is a coffee break; the same tree goes in one round trip this way.
    /// </summary>
    /// <param name="entry">The folder to remove.</param>
    /// <returns><c>false</c> when the server has no shell to run it, leaving the tree to be walked.</returns>
    public bool TryDeleteTree(FileEntry entry)
    {
        if (!entry.IsFolder)
        {
            return false;
        }

        lock (_shellGate)
        {
            if (Session() is not { } shell || Dialect(shell).Sweep(entry.Path) is not { } command)
            {
                return false;
            }

            try
            {
                using var running = shell.RunCommand(command);

                return running.ExitStatus == 0;
            }
            catch (Exception error) when (IsShellFailure(error))
            {
                _shellRefused = true;

                return false;
            }
        }
    }

    /// <summary>
    /// Asks whatever is answering how it spells a hard link. It is worked out before
    /// <see cref="Run"/> is called rather than inside it, so the two never hold the gate at once.
    /// </summary>
    /// <param name="path">Where the link goes.</param>
    /// <param name="target">What it points at.</param>
    /// <returns>The command, or <c>null</c> when there is no shell or it cannot make one.</returns>
    private string? Linking(string path, string target)
    {
        lock (_shellGate)
        {
            return Session() is { } shell ? Dialect(shell).Link(path, target) : null;
        }
    }

    /// <summary>
    /// Runs a command on the server over the session already open, in the folder the panel is showing.
    /// </summary>
    /// <param name="command">What was typed.</param>
    /// <param name="folder">Where to run it.</param>
    /// <returns>What it said and how it ended, or <c>null</c> when the server offers no shell.</returns>
    public (string Output, int Status)? Run(string command, string folder)
    {
        lock (_shellGate)
        {
            if (Session() is not { } shell)
            {
                return null;
            }

            try
            {
                using var running = shell.RunCommand(Dialect(shell).Within(folder, command));

                return (running.Result + running.Error, running.ExitStatus ?? -1);
            }
            catch (Exception error) when (IsShellFailure(error))
            {
                _shellRefused = true;

                return null;
            }
        }
    }

    /// <summary>
    /// What answers on the other side. A Unix server is a Unix server, but an OpenSSH server on
    /// Windows hands the command to whatever was set as its default shell — <c>cmd.exe</c> on a stock
    /// install, PowerShell on many others — and the three of them share no way of removing a folder.
    /// Asked once per connection and remembered.
    /// </summary>
    private Shell Dialect(SshClient shell)
    {
        if (_dialect is not null)
        {
            return _dialect;
        }

        _dialect = Shell.Ask(question =>
        {
            try
            {
                using var running = shell.RunCommand(question);

                return (running.Result + running.Error, running.ExitStatus ?? -1);
            }
            catch (Exception error) when (IsShellFailure(error))
            {
                return ("", -1);
            }
        });

        return _dialect;
    }

    /// <summary>
    /// The shell session, opened once and kept. Opening one costs a round trip, which is the whole
    /// saving when a hundred small folders are removed one after another.
    /// </summary>
    /// <returns>The session, or <c>null</c> when the server would not give one.</returns>
    private SshClient? Session()
    {
        if (_shellRefused)
        {
            return null;
        }

        if (_session is { IsConnected: true })
        {
            return _session;
        }

        try
        {
            _session?.Dispose();
            _session = new(Credentials.For(_connection));
            _session.Connect();

            return _session;
        }
        catch (Exception error) when (IsShellFailure(error))
        {
            _session = null;
            _shellRefused = true;

            return null;
        }
    }

    private static bool IsShellFailure(Exception error) =>
        error is SshException or SocketException or IOException or ObjectDisposedException
            or UnauthorizedAccessException;

    public void Move(string from, string target)
    {
        using var lease = _pool.Take();
        var client = lease.Client;

        Guarded(() => client.RenameFile(from, target));
    }

    public void Dispose()
    {
        lock (_shellGate)
        {
            _session?.Dispose();
            _session = null;
        }

        _pool.Dispose();
    }

    private static LeasedStream Leased(SftpPool.Lease lease, Func<Stream> open)
    {
        try
        {
            return new(Guarded(open), lease);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private static T Guarded<T>(Func<T> work)
    {
        try
        {
            return work();
        }
        catch (Exception error) when (error is SshException or ObjectDisposedException or SocketException)
        {
            throw RemotePaths.AsIoException(error);
        }
    }

    private static void Guarded(Action work) => Guarded<object?>(() =>
    {
        work();
        return null;
    });
}
