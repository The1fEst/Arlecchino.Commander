using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Files.Ssh;
using Arlecchino.Commander.Files.Work;

namespace Arlecchino.Commander.Files.Sources;

/// <summary>
/// A server reached over FTP, whose one control connection answers a request at a time. Every request waits
/// its turn on a semaphore, since a lock cannot be held across the wait for a reply.
/// </summary>
public sealed class FtpSource : IFileSource
{
    private readonly SemaphoreSlim _turn = new(1, 1);
    private readonly FtpConnection _client;
    private readonly Connection _connection;

    private FtpSource(Connection connection, FtpConnection client)
    {
        _connection = connection;
        _client = client;
    }

    public string Label => _connection.Label;

    public bool IsRemote => true;

    /// <summary>One: a control connection answers one request at a time, in the order they were asked.</summary>
    public int Concurrency => 1;

    public string Home => RemotePaths.Root;

    public bool WalksCheaply => false;

    public static async Task<FtpSource> ConnectAsync(Connection connection, CancellationToken token)
    {
        try
        {
            var client = await FtpConnection
                .OpenAsync(connection.Host, connection.Port, connection.User, connection.Password, token)
                .ConfigureAwait(false);

            return new(connection, client);
        }
        catch (Exception error) when (IsExpected(error))
        {
            throw RemotePaths.AsIoException(error);
        }
    }

    public Task<bool> TryDeleteTreeAsync(FileEntry entry, CancellationToken token) => Task.FromResult(false);

    /// <summary>
    /// The permissions the server reports, which it does only when it speaks the <c>MLSD</c> or
    /// <c>LIST</c> dialect that carries them; the rest say nothing and get an empty answer.
    /// </summary>
    /// <param name="entry">The file or folder.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns>The octal digits, or an empty string.</returns>
    public async Task<string> ModeAsync(FileEntry entry, CancellationToken token)
    {
        await _turn.WaitAsync(token).ConfigureAwait(false);

        try
        {
            var folder = RemotePaths.Parent(entry.Path) ?? RemotePaths.Root;

            foreach (var found in await _client.ListAsync(folder, token).ConfigureAwait(false))
            {
                if (found.Name == RemotePaths.NameOf(entry.Path) && found.Mode > 0)
                {
                    return found.Mode.ToString(CultureInfo.InvariantCulture);
                }
            }

            return "";
        }
        catch (Exception error) when (IsExpected(error))
        {
            return "";
        }
        finally
        {
            _turn.Release();
        }
    }

    public async Task<bool> TryChangeModeAsync(FileEntry entry, string mode, CancellationToken token)
    {
        if (Modes.AsDigits(mode) is not { } wanted)
        {
            return false;
        }

        await _turn.WaitAsync(token).ConfigureAwait(false);

        try
        {
            return await _client.TryChangeModeAsync(entry.Path, wanted, token).ConfigureAwait(false);
        }
        catch (Exception error) when (IsExpected(error))
        {
            return false;
        }
        finally
        {
            _turn.Release();
        }
    }

    /// <summary>FTP has no request for a link of either kind.</summary>
    /// <param name="path">Where the link would go.</param>
    /// <param name="target">What it would point at.</param>
    /// <param name="hard">Whether it would be a hard link.</param>
    /// <param name="token">Unused: nothing is asked of the server.</param>
    /// <returns>Always <c>false</c>.</returns>
    public Task<bool> TryLinkAsync(string path, string target, bool hard, CancellationToken token) =>
        Task.FromResult(false);

    /// <summary>A server has no trash, so nothing deleted there can be fetched back.</summary>
    public bool HasTrash => false;

    /// <summary>Nothing: there is nowhere on a server to put it.</summary>
    /// <param name="entry">The file or folder.</param>
    /// <param name="token">Unused: nothing is waited on.</param>
    /// <returns><c>false</c>, always.</returns>
    public Task<bool> TryTrashAsync(FileEntry entry, CancellationToken token) => Task.FromResult(false);

    /// <summary>
    /// The path as it stands. There is no shell here to read it back, so there are no rules to escape
    /// it by; what this returns is only ever shown, never run.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The same path.</returns>
    public string Quote(string path) => path;

    /// <summary>FTP has no shell, so nothing runs here.</summary>
    /// <param name="command">What was typed.</param>
    /// <param name="folder">The folder it would run in.</param>
    /// <returns><c>null</c>, always.</returns>
    public IShellRun? Start(string command, string folder) => null;

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
        await _turn.WaitAsync(token).ConfigureAwait(false);

        try
        {
            return await _client.FolderExistsAsync(folder, token).ConfigureAwait(false);
        }
        catch (Exception error) when (IsExpected(error))
        {
            return false;
        }
        finally
        {
            _turn.Release();
        }
    }

    public Task<string> FreeAsync(string folder, CancellationToken token) => Task.FromResult("");

    public async Task<IReadOnlyList<FileEntry>> ListAsync(string folder, bool showHidden, CancellationToken token)
    {
        await _turn.WaitAsync(token).ConfigureAwait(false);

        try
        {
            var entries = new List<FileEntry>();

            if (RemotePaths.Parent(folder) is { } parent)
            {
                entries.Add(new("..", parent, true, true, 0, default, false, false, false));
            }

            foreach (var found in await _client.ListAsync(folder, token).ConfigureAwait(false))
            {
                var hidden = RemotePaths.IsHidden(found.Name);

                if (hidden && !showHidden)
                {
                    continue;
                }

                entries.Add(new(
                    found.Name,
                    RemotePaths.Combine(folder, found.Name),
                    found.IsFolder,
                    false,
                    Math.Max(0, found.Size),
                    found.Modified,
                    hidden,
                    false,
                    !found.IsFolder && Modes.Runs(found.Mode)));
            }

            return entries;
        }
        catch (Exception error) when (IsExpected(error))
        {
            throw RemotePaths.AsIoException(error);
        }
        finally
        {
            _turn.Release();
        }
    }

    /// <summary>
    /// Opens a file to read, giving the turn back before the bytes. A transfer runs on its own connection,
    /// and holding the control connection through it would stop everything else.
    /// </summary>
    /// <param name="path">Which file.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns>The bytes.</returns>
    public async Task<Stream> OpenReadAsync(string path, CancellationToken token)
    {
        await _turn.WaitAsync(token).ConfigureAwait(false);

        try
        {
            return await _client.OpenReadAsync(path, token).ConfigureAwait(false);
        }
        catch (Exception error) when (IsExpected(error))
        {
            throw RemotePaths.AsIoException(error);
        }
        finally
        {
            _turn.Release();
        }
    }

    public async Task<Stream> CreateAsync(string path, CancellationToken token)
    {
        await _turn.WaitAsync(token).ConfigureAwait(false);

        try
        {
            return await _client.OpenWriteAsync(path, token).ConfigureAwait(false);
        }
        catch (Exception error) when (IsExpected(error))
        {
            throw RemotePaths.AsIoException(error);
        }
        finally
        {
            _turn.Release();
        }
    }

    public async Task CreateFolderAsync(string path, CancellationToken token)
    {
        await _turn.WaitAsync(token).ConfigureAwait(false);

        try
        {
            await _client.CreateFolderAsync(path, token).ConfigureAwait(false);
        }
        catch (Exception error) when (IsExpected(error))
        {
            throw RemotePaths.AsIoException(error);
        }
        finally
        {
            _turn.Release();
        }
    }

    public async Task DeleteAsync(FileEntry entry, CancellationToken token)
    {
        await _turn.WaitAsync(token).ConfigureAwait(false);

        try
        {
            if (entry.IsFolder)
            {
                await _client.DeleteFolderAsync(entry.Path, token).ConfigureAwait(false);

                return;
            }

            await _client.DeleteFileAsync(entry.Path, token).ConfigureAwait(false);
        }
        catch (Exception error) when (IsExpected(error))
        {
            throw RemotePaths.AsIoException(error);
        }
        finally
        {
            _turn.Release();
        }
    }

    public async Task MoveAsync(string from, string target, CancellationToken token)
    {
        await _turn.WaitAsync(token).ConfigureAwait(false);

        try
        {
            await _client.RenameAsync(from, target, token).ConfigureAwait(false);
        }
        catch (Exception error) when (IsExpected(error))
        {
            throw RemotePaths.AsIoException(error);
        }
        finally
        {
            _turn.Release();
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _turn.Dispose();
    }

    private static bool IsExpected(Exception error) =>
        error is IOException or SocketException or TimeoutException or ObjectDisposedException;
}
