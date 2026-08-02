using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files;
using Xunit;

namespace Arlecchino.Commander.Tests;

/// <summary>
/// The client against a server that answers the way the specification says. It proves the shape of
/// the conversation rather than that any real server agrees — for that the client was run against
/// vsftpd by hand, which is how the two bugs a server of our own could never have shown were found:
/// a byte order mark in front of the first command, and a server with no <c>MLSD</c> at all.
/// </summary>
public sealed class FtpConversationTests : IDisposable
{
    private readonly FakeFtpServer _server = new();

    public void Dispose() => _server.Dispose();

    [Fact]
    public async Task SigningInWalksThroughTheCommandsInOrder()
    {
        using (await Connect())
        {
        }

        Assert.Equal(
            ["USER demo", "PASS secret", "OPTS UTF8 ON", "TYPE I", "QUIT"],
            _server.Heard);
    }

    /// <summary>
    /// A greeting may run to several lines, and only a line beginning with the code and a space ends
    /// it. Reading one line and stopping would leave the rest to be taken for the answer to <c>USER</c>.
    /// </summary>
    [Fact]
    public async Task AGreetingOfSeveralLinesIsReadToItsEnd()
    {
        _server.Greeting = ["220-welcome", "220-still the banner", "220 ready"];

        using var ftp = await Connect();

        Assert.Equal("USER demo", _server.Heard[0]);
    }

    [Fact]
    public async Task AListingComesBackOverTheSecondConnection()
    {
        _server.Listing = "type=dir; docs\r\ntype=file;size=5; hello.txt\r\n";

        using var ftp = await Connect();
        var entries = await ftp.ListAsync("/pub", CancellationToken.None);

        Assert.Equal(2, entries.Count);
        Assert.True(entries[0].IsFolder);
        Assert.Equal(5, entries[1].Size);
        Assert.Contains("MLSD /pub", _server.Heard);
    }

    /// <summary>
    /// Not every server has <c>MLSD</c> — vsftpd answers <c>500</c> — so the older command has to be
    /// tried, and only once: asking again on every listing would double every round trip.
    /// </summary>
    [Fact]
    public async Task AServerWithoutTheMachineListingIsAskedTheOldWayInstead()
    {
        _server.KnowsMachineListing = false;
        _server.Listing = "drwxr-xr-x 2 ftp ftp 4096 Jan 01 12:00 docs\r\n";

        using var ftp = await Connect();

        Assert.Equal("docs", Assert.Single(await ftp.ListAsync("/pub", CancellationToken.None)).Name);

        _server.Heard.Clear();

        Assert.Single(await ftp.ListAsync("/pub", CancellationToken.None));
        Assert.DoesNotContain("MLSD /pub", _server.Heard);
    }

    [Fact]
    public async Task AFileIsReadOverTheSecondConnection()
    {
        _server.File = "hello";

        using var ftp = await Connect();
        await using var reading = await ftp.OpenReadAsync("/hello.txt", CancellationToken.None);

        Assert.Equal("hello", await new StreamReader(reading).ReadToEndAsync());
    }

    /// <summary>
    /// The reply that ends a transfer arrives on the control connection after the data connection
    /// closes. Left unread it would be handed to whatever asked next, and every answer from then on
    /// would belong to the question before it.
    /// </summary>
    [Fact]
    public async Task TheReplyEndingATransferIsReadBeforeAnythingElseIsAsked()
    {
        _server.File = "hello";

        using var ftp = await Connect();

        await using (var reading = await ftp.OpenReadAsync("/hello.txt", CancellationToken.None))
        {
            _ = await new StreamReader(reading).ReadToEndAsync();
        }

        await ftp.CreateFolderAsync("/made", CancellationToken.None);

        Assert.Contains("MKD /made", _server.Heard);
    }

    [Fact]
    public async Task WhatIsWrittenGoesOverTheSecondConnection()
    {
        using var ftp = await Connect();

        await using (var writing = await ftp.OpenWriteAsync("/put.txt", CancellationToken.None))
        {
            await writing.WriteAsync("written"u8.ToArray(), CancellationToken.None);
        }

        Assert.Contains("STOR /put.txt", _server.Heard);
        Assert.Equal("written", _server.Received);
    }

    [Fact]
    public async Task ARenameGoesOverInTwoHalves()
    {
        using var ftp = await Connect();

        await ftp.RenameAsync("/from", "/to", CancellationToken.None);

        Assert.Contains("RNFR /from", _server.Heard);
        Assert.Contains("RNTO /to", _server.Heard);
    }

    [Fact]
    public async Task ARefusalIsRaisedRatherThanPassedOver()
    {
        _server.Refuse = "MKD";

        using var ftp = await Connect();

        var thrown = await Assert.ThrowsAsync<IOException>(
            () => ftp.CreateFolderAsync("/made", CancellationToken.None));

        Assert.Contains("MKD", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFolderThatIsNotThereIsAnAnswerRatherThanAFault()
    {
        _server.Refuse = "CWD";

        using var ftp = await Connect();

        Assert.False(await ftp.FolderExistsAsync("/nope", CancellationToken.None));
    }

    private Task<FtpConnection> Connect() =>
        FtpConnection.OpenAsync("127.0.0.1", _server.Port, "demo", "secret", CancellationToken.None);
}

/// <summary>A server that answers by the book, and only as much of it as these tests ask about.</summary>
internal sealed class FakeFtpServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly Thread _thread;

    public FakeFtpServer()
    {
        _listener = new(IPAddress.Loopback, 0);
        _listener.Start();

        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _thread = new(Serve) { IsBackground = true };
        _thread.Start();
    }

    public int Port { get; }

    public List<string> Heard { get; } = [];

    public IReadOnlyList<string> Greeting { get; set; } = ["220 ready"];

    public string Listing { get; set; } = "";

    public string File { get; set; } = "";

    public string Received { get; private set; } = "";

    public bool KnowsMachineListing { get; set; } = true;

    public string Refuse { get; set; } = "";

    public void Dispose()
    {
        try
        {
            _listener.Stop();
        }
        catch (SocketException)
        {
        }

        _thread.Join(TimeSpan.FromSeconds(2));
    }

    private void Serve()
    {
        try
        {
            using var client = _listener.AcceptTcpClient();
            var stream = client.GetStream();
            var plain = new UTF8Encoding(false);
            using var reader = new StreamReader(stream, plain);
            using var writer = new StreamWriter(stream, plain);

            writer.AutoFlush = true;
            writer.NewLine = "\r\n";

            foreach (var line in Greeting)
            {
                writer.WriteLine(line);
            }

            Answer(reader, writer);
        }
        catch (Exception error) when (error is IOException or SocketException or ObjectDisposedException or
                                          InvalidOperationException)
        {
        }
    }

    private void Answer(StreamReader reader, StreamWriter writer)
    {
        TcpListener? data = null;

        while (reader.ReadLine() is { } line)
        {
            Heard.Add(line);

            var name = line.Split(' ')[0].ToUpperInvariant();

            if (name == Refuse)
            {
                writer.WriteLine("550 no");
                continue;
            }

            switch (name)
            {
                case "USER":
                    writer.WriteLine("331 need a password");
                    break;

                case "PASS":
                    writer.WriteLine("230 signed in");
                    break;

                case "OPTS" or "TYPE" or "MKD" or "DELE" or "RMD" or "CWD" or "SITE":
                    writer.WriteLine("200 fine");
                    break;

                case "RNFR":
                    writer.WriteLine("350 and where to");
                    break;

                case "RNTO":
                    writer.WriteLine("250 moved");
                    break;

                case "EPSV":
                    data = new(IPAddress.Loopback, 0);
                    data.Start();
                    writer.WriteLine($"229 Entering Extended Passive Mode (|||{((IPEndPoint)data.LocalEndpoint).Port}|)");
                    break;

                case "MLSD" when !KnowsMachineListing:
                    writer.WriteLine("500 Unknown command.");
                    break;

                case "MLSD" or "LIST":
                    Hand(writer, data, Listing);
                    data = null;
                    break;

                case "RETR":
                    Hand(writer, data, File);
                    data = null;
                    break;

                case "STOR":
                    Take(writer, data);
                    data = null;
                    break;

                case "QUIT":
                    writer.WriteLine("221 bye");
                    return;

                default:
                    writer.WriteLine("502 no idea");
                    break;
            }
        }
    }

    private static void Hand(StreamWriter writer, TcpListener? data, string payload)
    {
        writer.WriteLine("150 here it comes");

        using (var sending = data!.AcceptTcpClient())
        {
            var bytes = Encoding.UTF8.GetBytes(payload);

            sending.GetStream().Write(bytes, 0, bytes.Length);
        }

        data.Stop();
        writer.WriteLine("226 that was all");
    }

    private void Take(StreamWriter writer, TcpListener? data)
    {
        writer.WriteLine("150 send it over");

        using (var taking = data!.AcceptTcpClient())
        {
            Received = new StreamReader(taking.GetStream(), Encoding.UTF8).ReadToEnd();
        }

        data.Stop();
        writer.WriteLine("226 got it");
    }
}
