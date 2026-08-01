using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Arlecchino.Commander.Model;
namespace Arlecchino.Commander.Files;
public sealed class FtpSource : IFileSource
{
    private readonly Lock _gate = new();
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
    public bool TryDeleteTree(FileEntry entry) => false;
    public string Home => RemotePaths.Root;
    public static FtpSource Connect(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        try
        {
            return new(
                connection,
                FtpConnection.Open(connection.Host, connection.Port, connection.User, connection.Password));
        }
        catch (Exception error) when (IsExpected(error))
        {
            throw RemotePaths.AsIoException(error);
        }
    }
    /// <summary>
    /// The permissions the server reports, which it does only when it speaks the <c>MLSD</c> or
    /// <c>LIST</c> dialect that carries them; the rest say nothing and get an empty answer.
    /// </summary>
    /// <param name="entry">The file or folder.</param>
    /// <returns>The octal digits, or an empty string.</returns>
    public string Mode(FileEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            try
            {
                foreach (var found in _client.List(RemotePaths.Parent(entry.Path) ?? RemotePaths.Root))
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
        }
    }
    public bool TryChangeMode(FileEntry entry, string mode)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (Modes.AsDigits(mode) is not { } wanted)
        {
            return false;
        }
        lock (_gate)
        {
            try
            {
                return _client.TryChangeMode(entry.Path, wanted);
            }
            catch (Exception error) when (IsExpected(error))
            {
                return false;
            }
        }
    }
    /// <summary>FTP has no request for a link of either kind.</summary>
    /// <param name="path">Where the link would go.</param>
    /// <param name="target">What it would point at.</param>
    /// <param name="hard">Whether it would be a hard link.</param>
    /// <returns>Always <c>false</c>.</returns>
    public bool TryLink(string path, string target, bool hard) => false;
    /// <summary>FTP has no shell, so nothing runs here.</summary>
    /// <param name="command">What was typed.</param>
    /// <param name="folder">The folder it would run in.</param>
    /// <returns><c>null</c>, always.</returns>
    public IShellRun? Start(string command, string folder) => null;
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
        lock (_gate)
        {
            try
            {
                return _client.FolderExists(folder);
            }
            catch (Exception error) when (IsExpected(error))
            {
                return false;
            }
        }
    }
    public string Free(string folder) => "";
    public IReadOnlyList<FileEntry> List(string folder, bool showHidden)
    {
        lock (_gate)
        {
            return Guarded(() =>
            {
                var entries = new List<FileEntry>();
                if (RemotePaths.Parent(folder) is { } parent)
                {
                    entries.Add(new("..", parent, true, true, 0, default, false, false));
                }
                foreach (var found in _client.List(folder))
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
                        false));
                }
                return (IReadOnlyList<FileEntry>)entries;
            });
        }
    }
    public Stream OpenRead(string path)
    {
        lock (_gate)
        {
            return Guarded(() => _client.OpenRead(path));
        }
    }
    public Stream Create(string path)
    {
        lock (_gate)
        {
            return Guarded(() => _client.OpenWrite(path));
        }
    }
    public void CreateFolder(string path)
    {
        lock (_gate)
        {
            Guarded(() => _client.CreateFolder(path));
        }
    }
    public void Delete(FileEntry entry)
    {
        lock (_gate)
        {
            Guarded(() =>
            {
                if (entry.IsFolder)
                {
                    _client.DeleteFolder(entry.Path);
                    return;
                }
                _client.DeleteFile(entry.Path);
            });
        }
    }
    public void Move(string from, string target)
    {
        lock (_gate)
        {
            Guarded(() => _client.Rename(from, target));
        }
    }
    public void Dispose()
    {
        lock (_gate)
        {
            _client.Dispose();
        }
    }
    private static bool IsExpected(Exception error) =>
        error is IOException or SocketException or TimeoutException or ObjectDisposedException;
    private static T Guarded<T>(Func<T> work)
    {
        try
        {
            return work();
        }
        catch (Exception error) when (IsExpected(error))
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
