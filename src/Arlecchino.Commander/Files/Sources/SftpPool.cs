using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Renci.SshNet;
using Renci.SshNet.Common;
using Arlecchino.Commander.Files.Ssh;

namespace Arlecchino.Commander.Files.Sources;

/// <summary>
/// A handful of SFTP sessions to the same server, handed out one at a time, so the next request can leave
/// before the last has answered. Sessions are opened as they are needed and then kept.
/// </summary>
public sealed class SftpPool : IDisposable
{
    /// <summary>
    /// How much of a file is asked for in one request, which with the round trip is the whole of how fast one
    /// file moves. Sixty-four kilobytes is the ceiling this library states, not a preference.
    /// </summary>
    private const uint Packet = 64 * 1024;

    private readonly ConcurrentBag<SftpClient> _free = [];
    private readonly ConcurrentBag<SftpClient> _all = [];
    private readonly SemaphoreSlim _slots;
    private readonly Connection _connection;

    /// <summary>Opens the first session, so a server that refuses is reported before anything else.</summary>
    /// <param name="connection">Where to connect and with what.</param>
    /// <param name="size">How many sessions at most.</param>
    public SftpPool(Connection connection, int size)
    {
        _connection = connection;
        _slots = new(size, size);

        var first = Open();

        _all.Add(first);
        _free.Add(first);
    }

    /// <summary>Takes a session, opening one when none is free and the pool is not full yet.</summary>
    /// <returns>The session, given back when the lease is disposed.</returns>
    public Lease Take()
    {
        _slots.Wait();

        if (_free.TryTake(out var free))
        {
            return new(this, free);
        }

        var opened = Open();

        _all.Add(opened);

        return new(this, opened);
    }

    /// <summary>Closes every session.</summary>
    public void Dispose()
    {
        foreach (var client in _all)
        {
            client.Dispose();
        }

        _slots.Dispose();
    }

    private void Give(SftpClient client)
    {
        _free.Add(client);
        _slots.Release();
    }

    private SftpClient Open()
    {
        var client = new SftpClient(Credentials.For(_connection)) { BufferSize = Packet };
        var check = Credentials.Watch(client, _connection);

        try
        {
            client.Connect();

            return client;
        }
        catch (SshException) when (check.Refusal.Length > 0)
        {
            client.Dispose();

            throw new IOException(check.Refusal);
        }
        catch (SshAuthenticationException error)
        {
            client.Dispose();

            throw new UnauthorizedAccessException(error.Message, error);
        }
        catch (Exception error) when (error is SshException or SocketException or IOException)
        {
            client.Dispose();

            throw RemotePaths.AsIoException(error);
        }
    }

    /// <summary>One session, borrowed for as long as the lease is held.</summary>
    public readonly struct Lease : IDisposable
    {
        private readonly SftpPool _pool;

        internal Lease(SftpPool pool, SftpClient client)
        {
            _pool = pool;
            Client = client;
        }

        /// <summary>The session to work on.</summary>
        public SftpClient Client { get; }

        /// <summary>Gives the session back.</summary>
        public void Dispose() => _pool.Give(Client);
    }
}
