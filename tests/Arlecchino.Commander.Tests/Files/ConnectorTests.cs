using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Arlecchino.Commander.Files.Ssh;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
///     Signing in, and where it is allowed to happen. Everything here is about the calling thread rather
///     than about what the connection ends up being.
/// </summary>
public sealed class ConnectorTests
{
    private const int Patience = 2000;

    /// <summary>
    ///     Starting a connection hands the caller straight back, rather than blocking the frame loop for
    ///     the handshake. The server here accepts the socket and then sends no banner at all.
    /// </summary>
    [Fact]
    public void StartingAConnectionDoesNotBlockTheCaller()
    {
        using var mute = new MuteServer();
        var answered = new ManualResetEventSlim();
        var wanted = new Connection(Protocol.Sftp, "127.0.0.1", mute.Port, "someone", "secret", "/");

        var clock = Stopwatch.StartNew();

        Connector.Start(wanted, (_, _) => answered.Set(), (_, _) => answered.Set());

        var handedBack = clock.ElapsedMilliseconds;

        Assert.True(
            handedBack < Patience,
            $"Start held its caller for {handedBack}ms while the handshake was still going");

        Assert.False(answered.IsSet, "the connection was still in flight, so nothing can have answered yet");
    }

    /// <summary>A socket that accepts and then keeps quiet, so whoever connects waits on a banner.</summary>
    private sealed class MuteServer : IDisposable
    {
        private readonly TcpListener _listener;

        public MuteServer()
        {
            _listener = new(IPAddress.Loopback, 0);
            _listener.Start();

            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        }

        public int Port { get; }

        public void Dispose()
        {
            _listener.Stop();
        }
    }
}
