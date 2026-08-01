using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Arlecchino.Commander.Files;

/// <summary>What the server said back: the three digits and the text after them.</summary>
/// <param name="Code">The reply code. A leading 1 means it has begun, 2 that it is done, 4 and 5 that it is not.</param>
/// <param name="Text">Everything it said, newlines and all.</param>
public readonly record struct FtpReply(int Code, string Text)
{
    /// <summary>Whether the code is one of the two that mean the request was carried out.</summary>
    public bool Worked => Code is >= 100 and < 400;
}

/// <summary>One entry of a listing, as much of it as the server was willing to say.</summary>
/// <param name="Name">Its name, without the folder.</param>
/// <param name="IsFolder">Whether it holds other entries.</param>
/// <param name="Size">How large it is, or nought.</param>
/// <param name="Modified">When it changed, or the default when the server did not say.</param>
/// <param name="Mode">The permissions as octal digits, or nought when the server did not say.</param>
public readonly record struct FtpEntry(string Name, bool IsFolder, long Size, DateTime Modified, int Mode);

/// <summary>
/// The part of FTP a file manager needs, and no more of it. The protocol is old and plain: commands
/// go over one connection as lines of text, and anything longer than a reply — a listing, a file —
/// goes over a second connection opened for that alone.
///
/// This exists rather than a library because what is wanted here is a dozen commands, and the
/// libraries that speak the whole of FTP bring a megabyte and their own trim warnings to do it.
///
/// Not thread-safe, and cannot be: a control connection answers one request at a time and the reply
/// belongs to whoever asked. <see cref="FtpSource"/> holds the lock.
/// </summary>
public sealed class FtpConnection : IDisposable
{
    private const int Chunk = 81920;

    private readonly TcpClient _control;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly string _host;

    private bool _machineListing = true;

    /// <summary>
    /// The encoding commands go out in. It has to be one built here rather than
    /// <see cref="Encoding.UTF8"/>, which writes a byte order mark before the first thing written —
    /// and a server reading a command line finds three bytes in front of <c>USER</c> and does not
    /// know the command.
    /// </summary>
    private static readonly UTF8Encoding Plain = new(encoderShouldEmitUTF8Identifier: false);

    private FtpConnection(TcpClient control, string host)
    {
        _control = control;
        _host = host;

        var stream = control.GetStream();

        _reader = new(stream, Plain, false, Chunk, leaveOpen: true);
        _writer = new(stream, Plain, Chunk, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
    }

    /// <summary>Opens the control connection and signs in.</summary>
    /// <param name="host">Where the server is.</param>
    /// <param name="port">Which port it listens on.</param>
    /// <param name="user">Who to sign in as.</param>
    /// <param name="password">The password.</param>
    /// <returns>The connection, ready for commands.</returns>
    /// <exception cref="IOException">The server refused, or never answered.</exception>
    public static FtpConnection Open(string host, int port, string user, string password)
    {
        var control = new TcpClient();

        try
        {
            control.Connect(host, port <= 0 ? 21 : port);
        }
        catch (Exception error) when (error is SocketException or ArgumentException)
        {
            control.Dispose();

            throw new IOException($"could not reach {host}: {error.Message}", error);
        }

        var connection = new FtpConnection(control, host);

        try
        {
            Expect(connection.Read(), "the greeting");
            Expect(connection.Send($"USER {user}"), "USER", allow: 331);
            Expect(connection.Send($"PASS {password}"), "PASS");

            connection.Send("OPTS UTF8 ON");
            Expect(connection.Send("TYPE I"), "TYPE I");

            return connection;
        }
        catch
        {
            connection.Dispose();

            throw;
        }
    }

    /// <summary>Lists a folder.</summary>
    /// <param name="folder">Which folder.</param>
    /// <returns>What is in it, without <c>.</c> and <c>..</c>.</returns>
    /// <exception cref="IOException">The server refused, or never answered.</exception>
    public IReadOnlyList<FtpEntry> List(string folder)
    {
        if (!_machineListing)
        {
            return FtpListings.Plain(Listing("LIST", folder) ?? "");
        }

        if (Listing("MLSD", folder) is { } machine)
        {
            return FtpListings.Machine(machine);
        }

        _machineListing = false;

        return FtpListings.Plain(Listing("LIST", folder) ?? "");
    }

    /// <summary>Whether the folder is there and can be entered.</summary>
    /// <param name="folder">Which folder.</param>
    /// <returns><c>true</c> when the server changed into it.</returns>
    public bool FolderExists(string folder) => Send($"CWD {folder}").Worked;

    /// <summary>Makes a folder.</summary>
    /// <param name="path">Where it goes.</param>
    /// <exception cref="IOException">The server refused.</exception>
    public void CreateFolder(string path) => Expect(Send($"MKD {path}"), "MKD");

    /// <summary>Removes a file.</summary>
    /// <param name="path">Which file.</param>
    /// <exception cref="IOException">The server refused.</exception>
    public void DeleteFile(string path) => Expect(Send($"DELE {path}"), "DELE");

    /// <summary>Removes a folder, which the server refuses unless it is empty.</summary>
    /// <param name="path">Which folder.</param>
    /// <exception cref="IOException">The server refused.</exception>
    public void DeleteFolder(string path) => Expect(Send($"RMD {path}"), "RMD");

    /// <summary>Moves or renames an entry, which FTP asks for in two halves.</summary>
    /// <param name="from">Where it is now.</param>
    /// <param name="target">Where it is going.</param>
    /// <exception cref="IOException">The server refused either half.</exception>
    public void Rename(string from, string target)
    {
        Expect(Send($"RNFR {from}"), "RNFR", allow: 350);
        Expect(Send($"RNTO {target}"), "RNTO");
    }

    /// <summary>Sets the permissions, which is not FTP itself but a <c>SITE</c> command most servers have.</summary>
    /// <param name="path">The file or folder.</param>
    /// <param name="mode">The octal digits.</param>
    /// <returns><c>false</c> when the server has no such command.</returns>
    public bool TryChangeMode(string path, int mode) =>
        Send($"SITE CHMOD {mode:000} {path}").Worked;

    /// <summary>Opens a file to read.</summary>
    /// <param name="path">Which file.</param>
    /// <returns>The bytes. Closing it finishes the transfer with the server.</returns>
    /// <exception cref="IOException">The server refused, or never answered.</exception>
    public Stream OpenRead(string path) => Transfer($"RETR {path}");

    /// <summary>Opens a file to write, replacing whatever was there.</summary>
    /// <param name="path">Which file.</param>
    /// <returns>Where to write. Closing it finishes the transfer with the server.</returns>
    /// <exception cref="IOException">The server refused, or never answered.</exception>
    public Stream OpenWrite(string path) => Transfer($"STOR {path}");

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            Send("QUIT");
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or SocketException)
        {
        }

        _reader.Dispose();
        _writer.Dispose();
        _control.Dispose();
    }

    /// <summary>
    /// Runs a command whose answer comes over a data connection, and hands back the text of it.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="folder">The folder to run it on.</param>
    /// <returns>What came over, or <c>null</c> when the server said it does not know the command.</returns>
    private string? Listing(string command, string folder)
    {
        using var data = OpenData();

        var began = Send($"{command} {folder}");

        if (began.Code is 500 or 501 or 502)
        {
            return null;
        }

        Expect(began, command);

        using var reading = new StreamReader(data.GetStream(), Plain);
        var text = reading.ReadToEnd();

        data.Close();
        Expect(Read(), command);

        return text;
    }

    private FtpStream Transfer(string command)
    {
        var data = OpenData();

        try
        {
            Expect(Send(command), command);
        }
        catch
        {
            data.Dispose();

            throw;
        }

        return new(data, this);
    }

    /// <summary>
    /// Opens the second connection. <c>EPSV</c> is asked for first because it carries only a port and
    /// so says nothing about addresses the client cannot reach; <c>PASV</c> is the older spelling that
    /// every server has, and it answers with an address that is sometimes the one behind the router
    /// rather than the one in front of it — so the host already connected to is used instead.
    /// </summary>
    /// <returns>The data connection.</returns>
    /// <exception cref="IOException">The server would not open one.</exception>
    private TcpClient OpenData()
    {
        var extended = Send("EPSV");

        var port = extended.Worked
            ? FtpListings.ExtendedPort(extended.Text)
            : FtpListings.PassivePort(Expect(Send("PASV"), "PASV").Text);

        if (port <= 0)
        {
            throw new IOException("the server would not say where to send the data");
        }

        var data = new TcpClient();

        try
        {
            data.Connect(_host, port);

            return data;
        }
        catch (Exception error) when (error is SocketException or ArgumentException)
        {
            data.Dispose();

            throw new IOException($"could not open the data connection: {error.Message}", error);
        }
    }

    /// <summary>Reads the reply that ends a transfer, once the data connection has closed.</summary>
    internal void Finish() => Read();

    private FtpReply Send(string command)
    {
        try
        {
            _writer.WriteLine(command);
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException)
        {
            throw new IOException($"the connection went away while sending {Named(command)}", error);
        }

        return Read();
    }

    /// <summary>
    /// Reads one reply. A reply is one line, unless the code is followed by a dash — then it runs
    /// until a line begins with the same code and a space, which is the only way to know it has ended.
    /// </summary>
    /// <returns>The code and the text.</returns>
    /// <exception cref="IOException">The connection went away mid-reply.</exception>
    private FtpReply Read()
    {
        var first = Line();
        var code = Code(first);

        if (code == 0 || first.Length < 4 || first[3] != '-')
        {
            return new(code, first);
        }

        var text = new StringBuilder(first);
        var ending = $"{code} ";

        while (true)
        {
            var next = Line();

            text.Append('\n').Append(next);

            if (next.StartsWith(ending, StringComparison.Ordinal))
            {
                return new(code, text.ToString());
            }
        }
    }

    private string Line()
    {
        string? line;

        try
        {
            line = _reader.ReadLine();
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException)
        {
            throw new IOException("the connection went away while reading the answer", error);
        }

        return line ?? throw new IOException("the server closed the connection");
    }

    private static int Code(string line) =>
        line.Length >= 3 && int.TryParse(line[..3], NumberStyles.None, CultureInfo.InvariantCulture, out var code)
            ? code
            : 0;

    private static FtpReply Expect(FtpReply reply, string what, int allow = 0)
    {
        if (reply.Worked || reply.Code == allow)
        {
            return reply;
        }

        throw new IOException($"{what} was refused: {reply.Text}");
    }

    private static string Named(string command) =>
        command.Split(' ', 2)[0];
}

/// <summary>
/// The bytes of one transfer, as a stream over the data connection.
///
/// Closing it is not the formality it is on a file. The socket closing is what tells the server the
/// transfer has ended, and the server answers that on the control connection — so the reply has to be
/// read here and now. Left unread it would be handed to whatever asked next, which would then be
/// reading the answer to somebody else's question for the rest of the session.
/// </summary>
internal sealed class FtpStream : Stream
{
    private readonly TcpClient _data;
    private readonly NetworkStream _stream;
    private readonly FtpConnection _connection;

    private bool _closed;

    public FtpStream(TcpClient data, FtpConnection connection)
    {
        _data = data;
        _stream = data.GetStream();
        _connection = connection;
    }

    public override bool CanRead => _stream.CanRead;

    public override bool CanWrite => _stream.CanWrite;

    public override bool CanSeek => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);

    public override void Write(byte[] buffer, int offset, int count) => _stream.Write(buffer, offset, count);

    public override void Flush() => _stream.Flush();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (!disposing || _closed)
        {
            base.Dispose(disposing);
            return;
        }

        _closed = true;

        _stream.Dispose();
        _data.Dispose();

        try
        {
            _connection.Finish();
        }
        catch (IOException)
        {
        }

        base.Dispose(disposing);
    }
}
