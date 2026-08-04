using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Model;
using Renci.SshNet;
using Shell = Arlecchino.Commander.Files.Ssh.Shell;
using Renci.SshNet.Common;
using Arlecchino.Commander.Files.Ssh;
using Arlecchino.Commander.Files.Work;

namespace Arlecchino.Commander.Files.Sources;

public sealed class SftpSource : IFileSource, IMovesWholeFiles
{
    private const int Sessions = 8;

    private readonly SftpPool _pool;
    private readonly Connection _connection;
    private readonly SemaphoreSlim _shellGate = new(1, 1);

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

    public async Task<bool> FolderExistsAsync(string folder, CancellationToken token)
    {
        using var lease = _pool.Take();
        var client = lease.Client;

        try
        {
            return await client.ExistsAsync(folder, token).ConfigureAwait(false) &&
                   (await client.GetAttributesAsync(folder, token).ConfigureAwait(false)).IsDirectory;
        }
        catch (Exception error) when (error is SshException or ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>
    /// How much room is left where the panel is looking. SFTP itself has no request for it, but OpenSSH
    /// answers one of its own, and every server worth connecting to is OpenSSH. A server that is not
    /// simply refuses, and the footer goes back to saying nothing rather than saying something wrong.
    ///
    /// The blocks are counted in <c>BlockSize</c>, which despite the name is the fundamental size the
    /// counts are quoted in; <c>FileSystemBlockSize</c> is the size a transfer would prefer and has
    /// nothing to do with how much is left. Blocks available, not blocks free: the difference between
    /// the two is the share of the disk kept back for root, which nobody logging in as anyone else
    /// can have.
    /// </summary>
    /// <param name="folder">Where the panel is looking.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns>The line for the footer, or nothing when the server would not answer.</returns>
    public async Task<string> FreeAsync(string folder, CancellationToken token)
    {
        using var lease = _pool.Take();

        try
        {
            var status = await lease.Client.GetStatusAsync(folder, token).ConfigureAwait(false);
            var free = status.AvailableBlocks * status.BlockSize;

            return Sizes.Free(free > long.MaxValue ? long.MaxValue : (long)free);
        }
        catch (Exception error) when (error is SshException or ObjectDisposedException or
                                          SocketException or NotSupportedException)
        {
            return "";
        }
    }

    public async Task<string> ModeAsync(FileEntry entry, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(entry);

        using var lease = _pool.Take();
        var client = lease.Client;

        try
        {
            var attributes = await client.GetAttributesAsync(entry.Path, token).ConfigureAwait(false);

            return Modes.Write(RemotePaths.ModeOf(attributes));
        }
        catch (Exception error) when (error is SshException or ObjectDisposedException)
        {
            return "";
        }
    }

    /// <summary>
    /// Sets the permissions. SFTP has a request for it and the library has no waiting form of that
    /// request, so this is the one place a round trip is spent on the thread that asked — it is one
    /// request, and it is never on the drawing thread.
    /// </summary>
    /// <param name="entry">The file or folder.</param>
    /// <param name="mode">The octal digits, as typed.</param>
    /// <param name="token">Unused: there is nothing here to give up on.</param>
    /// <returns><c>false</c> when the digits were not digits, or the server refused.</returns>
    public Task<bool> TryChangeModeAsync(FileEntry entry, string mode, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (Modes.AsDigits(mode) is not { } wanted)
        {
            return Task.FromResult(false);
        }

        using var lease = _pool.Take();
        var client = lease.Client;

        try
        {
            client.ChangePermissions(entry.Path, (short)wanted);

            return Task.FromResult(true);
        }
        catch (Exception error) when (error is SshException or ObjectDisposedException)
        {
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Makes a link on the server. SFTP has a request of its own for a symbolic link; a hard link it
    /// has none for, so that one is asked of the shell.
    /// </summary>
    /// <param name="path">Where the link goes.</param>
    /// <param name="target">What it points at.</param>
    /// <param name="hard">Whether it is a hard link.</param>
    /// <param name="token">Gives up waiting on the shell a hard link needs.</param>
    /// <returns><c>false</c> when the server refused it.</returns>
    public async Task<bool> TryLinkAsync(string path, string target, bool hard, CancellationToken token)
    {
        if (hard)
        {
            return await LinkingAsync(path, target, token).ConfigureAwait(false) is { } command &&
                   await RunAsync(command, RemotePaths.Parent(path) ?? RemotePaths.Root, token)
                       .ConfigureAwait(false) is { Status: 0 };
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

    public async Task<IReadOnlyList<FileEntry>> ListAsync(string folder, bool showHidden, CancellationToken token)
    {
        using var lease = _pool.Take();
        var client = lease.Client;
        var entries = new List<FileEntry>();

        if (RemotePaths.Parent(folder) is { } parent)
        {
            entries.Add(new("..", parent, true, true, 0, default, false, false));
        }

        try
        {
            await foreach (var found in client.ListDirectoryAsync(folder, token).ConfigureAwait(false))
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
        }
        catch (Exception error) when (error is SshException or ObjectDisposedException or SocketException)
        {
            throw RemotePaths.AsIoException(error);
        }

        return entries;
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken token)
    {
        var lease = _pool.Take();

        return LeasedAsync(lease, () => lease.Client.OpenAsync(path, FileMode.Open, FileAccess.Read, token));
    }

    public Task<Stream> CreateAsync(string path, CancellationToken token)
    {
        var lease = _pool.Take();

        return LeasedAsync(lease, () => lease.Client.OpenAsync(path, FileMode.Create, FileAccess.Write, token));
    }

    public async Task CreateFolderAsync(string path, CancellationToken token)
    {
        using var lease = _pool.Take();
        var client = lease.Client;

        try
        {
            if (!await client.ExistsAsync(path, token).ConfigureAwait(false))
            {
                await client.CreateDirectoryAsync(path, token).ConfigureAwait(false);
            }
        }
        catch (Exception error) when (error is SshException or ObjectDisposedException or SocketException)
        {
            throw RemotePaths.AsIoException(error);
        }
    }

    public async Task DeleteAsync(FileEntry entry, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(entry);

        using var lease = _pool.Take();
        var client = lease.Client;

        try
        {
            if (entry.IsFolder)
            {
                await client.DeleteDirectoryAsync(entry.Path, token).ConfigureAwait(false);

                return;
            }

            await client.DeleteFileAsync(entry.Path, token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is SshException or ObjectDisposedException or SocketException)
        {
            throw RemotePaths.AsIoException(error);
        }
    }

    /// <summary>
    /// Removes the tree with one command over SSH rather than a request per file. A server on
    /// the other side of the world answers a delete in a fraction of a second, which a thousand times
    /// over is a coffee break; the same tree goes in one round trip this way.
    ///
    /// Whether it worked is read from the exit status, so only a shell whose status means something
    /// may offer a command here — <c>rm</c> and <c>Remove-Item</c> both answer one on a refusal, and
    /// <see cref="WindowsCommandShell"/> offers none for exactly that reason.
    /// </summary>
    /// <param name="entry">The folder to remove.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns><c>false</c> when the server has no shell to run it, leaving the tree to be walked.</returns>
    public async Task<bool> TryDeleteTreeAsync(FileEntry entry, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!entry.IsFolder)
        {
            return false;
        }

        await _shellGate.WaitAsync(token).ConfigureAwait(false);

        try
        {
            if (await SessionAsync(token).ConfigureAwait(false) is not { } shell ||
                (await DialectAsync(shell, token).ConfigureAwait(false)).Sweep(entry.Path) is not { } command)
            {
                return false;
            }

            return await SaidAsync(shell, command, token).ConfigureAwait(false) is { Status: 0 };
        }
        finally
        {
            _shellGate.Release();
        }
    }

    /// <summary>
    /// Asks whatever is answering how it spells a hard link. It is worked out before
    /// <see cref="RunAsync"/> is called rather than inside it, so the two never hold the gate at once.
    /// </summary>
    /// <param name="path">Where the link goes.</param>
    /// <param name="target">What it points at.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns>The command, or <c>null</c> when there is no shell or it cannot make one.</returns>
    private async Task<string?> LinkingAsync(string path, string target, CancellationToken token)
    {
        await _shellGate.WaitAsync(token).ConfigureAwait(false);

        try
        {
            return await SessionAsync(token).ConfigureAwait(false) is { } shell
                ? (await DialectAsync(shell, token).ConfigureAwait(false)).Link(path, target)
                : null;
        }
        finally
        {
            _shellGate.Release();
        }
    }

    /// <summary>
    /// Runs a command on the server over the session already open, in the folder the panel is showing.
    /// </summary>
    /// <param name="command">What was typed.</param>
    /// <param name="folder">Where to run it.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns>What it said and how it ended, or <c>null</c> when the server offers no shell.</returns>
    public async Task<(string Output, int Status)?> RunAsync(string command, string folder, CancellationToken token)
    {
        await _shellGate.WaitAsync(token).ConfigureAwait(false);

        try
        {
            if (await SessionAsync(token).ConfigureAwait(false) is not { } shell)
            {
                return null;
            }

            var dialect = await DialectAsync(shell, token).ConfigureAwait(false);

            return await SaidAsync(shell, dialect.Within(folder, command), token).ConfigureAwait(false);
        }
        finally
        {
            _shellGate.Release();
        }
    }

    /// <summary>
    /// Sends one command over the session already open and waits for it to finish. A server that goes
    /// quiet or refuses the session is remembered as having no shell, so the next command walks the
    /// long way round rather than waiting on the same refusal again.
    /// </summary>
    /// <param name="shell">The session.</param>
    /// <param name="command">What to send.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns>What it said and how it ended, or <c>null</c> when the session failed.</returns>
    private async Task<(string Output, int Status)?> SaidAsync(
        SshClient shell,
        string command,
        CancellationToken token)
    {
        try
        {
            using var running = shell.CreateCommand(command);

            await running.ExecuteAsync(token).ConfigureAwait(false);

            return (running.Result + running.Error, running.ExitStatus ?? -1);
        }
        catch (Exception error) when (IsShellFailure(error))
        {
            _shellRefused = true;

            return null;
        }
    }

    /// <summary>
    /// What answers on the other side. A Unix server is a Unix server, but an OpenSSH server on
    /// Windows hands the command to whatever was set as its default shell — <c>cmd.exe</c> on a stock
    /// install, PowerShell on many others — and the three of them share no way of removing a folder.
    /// Asked once per connection and remembered.
    /// </summary>
    /// <param name="shell">The session to ask over.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns>What is answering.</returns>
    private async Task<Shell> DialectAsync(SshClient shell, CancellationToken token)
    {
        if (_dialect is not null)
        {
            return _dialect;
        }

        _dialect = await Shell
            .AskAsync(async question =>
                await SaidAsync(shell, question, token).ConfigureAwait(false) ?? ("", -1))
            .ConfigureAwait(false);

        return _dialect;
    }

    /// <summary>
    /// The shell session, opened once and kept. Opening one costs a round trip, which is the whole
    /// saving when a hundred small folders are removed one after another.
    /// </summary>
    /// <param name="token">Gives up the wait.</param>
    /// <returns>The session, or <c>null</c> when the server would not give one.</returns>
    private async Task<SshClient?> SessionAsync(CancellationToken token)
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

            Credentials.Watch(_session, _connection);

            await _session.ConnectAsync(token).ConfigureAwait(false);

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

    /// <summary>
    /// Moves something to a name that may already be taken. The plain SFTP rename refuses to land on an
    /// anything that exists, which made this the one operation in the program that stopped at a name
    /// already taken: a disk overwrites on a move, and a copy overwrites on either end.
    ///
    /// So the plain rename is asked first — it is the asynchronous one, it is a single round trip, and
    /// it is what succeeds whenever the name is free. Only when it refuses is the OpenSSH rename tried,
    /// which replaces whatever is there in one step, with no moment in between where neither the old
    /// name nor the new one holds the file. That one the library offers in no waiting form, so the
    /// second attempt spends its round trip on this thread; it is reached only by a move onto an
    /// occupied name, and never on the drawing thread.
    ///
    /// A server without the extension refuses that one too, and the move fails saying so, rather than
    /// deleting what is in the way to make room — an unpicked deletion is not something to do on the
    /// strength of a failed rename.
    /// </summary>
    /// <param name="from">Where it is now.</param>
    /// <param name="target">Where it is going.</param>
    /// <param name="token">Gives up the wait.</param>
    public async Task MoveAsync(string from, string target, CancellationToken token)
    {
        using var lease = _pool.Take();
        var client = lease.Client;

        try
        {
            try
            {
                await client.RenameFileAsync(from, target, token).ConfigureAwait(false);
            }
            catch (SshException)
            {
                client.RenameFile(from, target, true);
            }
        }
        catch (Exception error) when (error is SshException or ObjectDisposedException or SocketException)
        {
            throw RemotePaths.AsIoException(error);
        }
    }

    public void Dispose()
    {
        _shellGate.Wait();

        try
        {
            _session?.Dispose();
            _session = null;
        }
        finally
        {
            _shellGate.Release();
        }

        _shellGate.Dispose();
        _pool.Dispose();
    }

    /// <inheritdoc/>
    public async Task SendAsync(Stream reading, string target, CancellationToken token)
    {
        using var lease = _pool.Take();

        try
        {
            await lease.Client.UploadFileAsync(reading, target, token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is SshException or ObjectDisposedException or SocketException)
        {
            throw RemotePaths.AsIoException(error);
        }
    }

    /// <inheritdoc/>
    public async Task FetchAsync(string source, Stream writing, CancellationToken token)
    {
        using var lease = _pool.Take();

        try
        {
            await lease.Client.DownloadFileAsync(source, writing, token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is SshException or ObjectDisposedException or SocketException)
        {
            throw RemotePaths.AsIoException(error);
        }
    }

    /// <summary>
    /// Opens a stream that holds its session until it is closed. The lease goes back to the pool with
    /// the stream rather than when this returns: the bytes have not been read yet, and a session handed
    /// back early is one another copy would take while this one is still using it.
    /// </summary>
    /// <param name="lease">The session the stream will use.</param>
    /// <param name="open">Asks the server for the stream.</param>
    /// <returns>The stream, holding the lease.</returns>
    private static async Task<Stream> LeasedAsync<T>(SftpPool.Lease lease, Func<Task<T>> open)
        where T : Stream
    {
        try
        {
            return new LeasedStream(await open().ConfigureAwait(false), lease);
        }
        catch (Exception error) when (error is SshException or ObjectDisposedException or SocketException)
        {
            lease.Dispose();

            throw RemotePaths.AsIoException(error);
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
}
