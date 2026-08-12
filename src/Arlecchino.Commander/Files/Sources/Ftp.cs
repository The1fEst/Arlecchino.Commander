using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Arlecchino.Commander.Files.Sources;

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
/// The dozen FTP commands a file manager needs: they go over one connection as lines of text, and anything
/// longer goes over a second. It is not thread-safe, and <see cref="FtpSource"/> holds the lock.
/// </summary>
public sealed class FtpConnection : IDisposable
{
    private const int Chunk = 81920;

    /// <summary>
    /// The encoding commands go out in, built here rather than taken from <see cref="Encoding.UTF8"/>, which
    /// would write a byte order mark in front of the first command.
    /// </summary>
    private static readonly UTF8Encoding Plain = new(false);

    private readonly TcpClient _control;
    private readonly string _host;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;

    private bool _machineListing = true;

    private FtpConnection(TcpClient control, string host)
    {
        _control = control;
        _host = host;

        var stream = control.GetStream();

        _reader = new(stream, Plain, false, Chunk, true);
        _writer = new(stream, Plain, Chunk, true) { AutoFlush = true, NewLine = "\r\n" };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            SendAsync("QUIT", CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or SocketException) { }

        _reader.Dispose();
        _writer.Dispose();
        _control.Dispose();
    }

    /// <summary>Opens the control connection and signs in.</summary>
    /// <param name="host">Where the server is.</param>
    /// <param name="port">Which port it listens on.</param>
    /// <param name="user">Who to sign in as.</param>
    /// <param name="password">The password.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns>The connection, ready for commands.</returns>
    /// <exception cref="IOException">The server refused, or never answered.</exception>
    public static async Task<FtpConnection> OpenAsync(
        string host,
        int port,
        string user,
        string password,
        CancellationToken token)
    {
        var control = new TcpClient();

        try
        {
            await control.ConnectAsync(host, port <= 0 ? 21 : port, token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is SocketException or ArgumentException)
        {
            control.Dispose();

            throw new IOException($"could not reach {host}: {error.Message}", error);
        }

        var connection = new FtpConnection(control, host);

        try
        {
            Expect(await connection.ReadAsync(token).ConfigureAwait(false), "the greeting");
            Expect(await connection.SendAsync($"USER {user}", token).ConfigureAwait(false), "USER", 331);
            Expect(await connection.SendAsync($"PASS {password}", token).ConfigureAwait(false), "PASS");

            await connection.SendAsync("OPTS UTF8 ON", token).ConfigureAwait(false);
            Expect(await connection.SendAsync("TYPE I", token).ConfigureAwait(false), "TYPE I");

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
    /// <param name="token">Gives up the wait.</param>
    /// <returns>What is in it, without <c>.</c> and <c>..</c>.</returns>
    /// <exception cref="IOException">The server refused, or never answered.</exception>
    public async Task<IReadOnlyList<FtpEntry>> ListAsync(string folder, CancellationToken token)
    {
        if (!_machineListing)
        {
            return FtpListings.Plain(await ListingAsync("LIST", folder, token).ConfigureAwait(false) ?? "");
        }

        if (await ListingAsync("MLSD", folder, token).ConfigureAwait(false) is { } machine)
        {
            return FtpListings.Machine(machine);
        }

        _machineListing = false;

        return FtpListings.Plain(await ListingAsync("LIST", folder, token).ConfigureAwait(false) ?? "");
    }

    /// <summary>Whether the folder is there and can be entered.</summary>
    /// <param name="folder">Which folder.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns><c>true</c> when the server changed into it.</returns>
    public async Task<bool> FolderExistsAsync(string folder, CancellationToken token)
    {
        return (await SendAsync($"CWD {folder}", token).ConfigureAwait(false)).Worked;
    }

    /// <summary>Makes a folder.</summary>
    /// <param name="path">Where it goes.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns>A task that finishes once the server has answered.</returns>
    /// <exception cref="IOException">The server refused.</exception>
    public async Task CreateFolderAsync(string path, CancellationToken token)
    {
        Expect(await SendAsync($"MKD {path}", token).ConfigureAwait(false), "MKD");
    }

    /// <summary>Removes a file.</summary>
    /// <param name="path">Which file.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns>A task that finishes once the server has answered.</returns>
    /// <exception cref="IOException">The server refused.</exception>
    public async Task DeleteFileAsync(string path, CancellationToken token)
    {
        Expect(await SendAsync($"DELE {path}", token).ConfigureAwait(false), "DELE");
    }

    /// <summary>Removes a folder, which the server refuses unless it is empty.</summary>
    /// <param name="path">Which folder.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns>A task that finishes once the server has answered.</returns>
    /// <exception cref="IOException">The server refused.</exception>
    public async Task DeleteFolderAsync(string path, CancellationToken token)
    {
        Expect(await SendAsync($"RMD {path}", token).ConfigureAwait(false), "RMD");
    }

    /// <summary>Moves or renames an entry, which FTP asks for in two halves.</summary>
    /// <param name="from">Where it is now.</param>
    /// <param name="target">Where it is going.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns>A task that finishes once both halves have been answered.</returns>
    /// <exception cref="IOException">The server refused either half.</exception>
    public async Task RenameAsync(string from, string target, CancellationToken token)
    {
        Expect(await SendAsync($"RNFR {from}", token).ConfigureAwait(false), "RNFR", 350);
        Expect(await SendAsync($"RNTO {target}", token).ConfigureAwait(false), "RNTO");
    }

    /// <summary>Sets the permissions, which is not FTP itself but a <c>SITE</c> command most servers have.</summary>
    /// <param name="path">The file or folder.</param>
    /// <param name="mode">The octal digits.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns><c>false</c> when the server has no such command.</returns>
    public async Task<bool> TryChangeModeAsync(string path, int mode, CancellationToken token)
    {
        return (await SendAsync($"SITE CHMOD {mode:000} {path}", token).ConfigureAwait(false)).Worked;
    }

    /// <summary>Opens a file to read.</summary>
    /// <param name="path">Which file.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns>The bytes. Closing it finishes the transfer with the server.</returns>
    /// <exception cref="IOException">The server refused, or never answered.</exception>
    public Task<Stream> OpenReadAsync(string path, CancellationToken token)
    {
        return TransferAsync($"RETR {path}", token);
    }

    /// <summary>Opens a file to write, replacing whatever was there.</summary>
    /// <param name="path">Which file.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns>Where to write. Closing it finishes the transfer with the server.</returns>
    /// <exception cref="IOException">The server refused, or never answered.</exception>
    public Task<Stream> OpenWriteAsync(string path, CancellationToken token)
    {
        return TransferAsync($"STOR {path}", token);
    }

    /// <summary>
    ///     Runs a command whose answer comes over a data connection, and hands back the text of it.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="folder">The folder to run it on.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns>What came over, or <c>null</c> when the server said it does not know the command.</returns>
    private async Task<string?> ListingAsync(string command, string folder, CancellationToken token)
    {
        using var data = await OpenDataAsync(token).ConfigureAwait(false);

        var began = await SendAsync($"{command} {folder}", token).ConfigureAwait(false);

        if (began.Code is 500 or 501 or 502)
        {
            return null;
        }

        Expect(began, command);

        using var reading = new StreamReader(data.GetStream(), Plain);
        var text = await reading.ReadToEndAsync(token).ConfigureAwait(false);

        data.Close();
        Expect(await ReadAsync(token).ConfigureAwait(false), command);

        return text;
    }

    private async Task<Stream> TransferAsync(string command, CancellationToken token)
    {
        var data = await OpenDataAsync(token).ConfigureAwait(false);

        try
        {
            Expect(await SendAsync(command, token).ConfigureAwait(false), command);
        }
        catch
        {
            data.Dispose();

            throw;
        }

        return new FtpStream(data, this);
    }

    /// <summary>
    /// Opens the second connection, asking <c>EPSV</c> first because it carries only a port. The older
    /// <c>PASV</c> answers with an address that may be unreachable, so the host already in hand is used.
    /// </summary>
    /// <param name="token">Canceled when the work is stopped.</param>
    /// <returns>The data connection.</returns>
    /// <exception cref="IOException">The server would not open one.</exception>
    private async Task<TcpClient> OpenDataAsync(CancellationToken token)
    {
        var extended = await SendAsync("EPSV", token).ConfigureAwait(false);

        var port = extended.Worked
            ? FtpListings.ExtendedPort(extended.Text)
            : FtpListings.PassivePort(Expect(await SendAsync("PASV", token).ConfigureAwait(false), "PASV").Text);

        if (port <= 0)
        {
            throw new IOException("the server would not say where to send the data");
        }

        var data = new TcpClient();

        try
        {
            await data.ConnectAsync(_host, port, token).ConfigureAwait(false);

            return data;
        }
        catch (Exception error) when (error is SocketException or ArgumentException)
        {
            data.Dispose();

            throw new IOException($"could not open the data connection: {error.Message}", error);
        }
    }

    /// <summary>Reads the reply that ends a transfer, once the data connection has closed.</summary>
    /// <returns>A task that finishes when the server has said the transfer is over.</returns>
    internal Task FinishAsync()
    {
        return ReadAsync(CancellationToken.None);
    }

    private async Task<FtpReply> SendAsync(string command, CancellationToken token)
    {
        try
        {
            await _writer.WriteLineAsync(command.AsMemory(), token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException)
        {
            throw new IOException($"the connection went away while sending {Named(command)}", error);
        }

        return await ReadAsync(token).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads one reply, which is one line unless the code is followed by a dash. Then it runs until a line
    /// begins with the same code and a space.
    /// </summary>
    /// <param name="token">Canceled when the work is stopped.</param>
    /// <returns>The code and the text.</returns>
    /// <exception cref="IOException">The connection went away mid-reply.</exception>
    private async Task<FtpReply> ReadAsync(CancellationToken token)
    {
        var first = await LineAsync(token).ConfigureAwait(false);
        var code = Code(first);

        if (code == 0 || first.Length < 4 || first[3] != '-')
        {
            return new(code, first);
        }

        var text = new StringBuilder(first);
        var ending = $"{code} ";

        while (true)
        {
            var next = await LineAsync(token).ConfigureAwait(false);

            text.Append('\n').Append(next);

            if (next.StartsWith(ending, StringComparison.Ordinal))
            {
                return new(code, text.ToString());
            }
        }
    }

    private async Task<string> LineAsync(CancellationToken token)
    {
        string? line;

        try
        {
            line = await _reader.ReadLineAsync(token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException)
        {
            throw new IOException("the connection went away while reading the answer", error);
        }

        return line ?? throw new IOException("the server closed the connection");
    }

    private static int Code(string line)
    {
        return line.Length >= 3 && int.TryParse(line[..3], NumberStyles.None, CultureInfo.InvariantCulture, out var code)
            ? code
            : 0;
    }

    private static FtpReply Expect(FtpReply reply, string what, int allow = 0)
    {
        if (reply.Worked || reply.Code == allow)
        {
            return reply;
        }

        throw new IOException($"{what} was refused: {reply.Text}");
    }

    private static string Named(string command)
    {
        return command.Split(' ', 2)[0];
    }
}

/// <summary>
/// The bytes of one transfer, as a stream over the data connection. Closing it is what tells the server the
/// transfer ended, and the reply to that has to be read before anything else asks a question.
/// </summary>
internal sealed class FtpStream : Stream
{
    private readonly FtpConnection _connection;
    private readonly TcpClient _data;
    private readonly NetworkStream _stream;

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

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _stream.Read(buffer, offset, count);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _stream.Write(buffer, offset, count);
    }

    /// <summary>
    ///     Reads without holding a thread. Left to the base class this would run the blocking read on a
    ///     pool thread and call it asynchronous, which for a transfer over a network is the whole cost.
    /// </summary>
    /// <param name="buffer">Where the bytes go.</param>
    /// <param name="token">Gives up the read.</param>
    /// <returns>How many bytes arrived.</returns>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
    {
        return _stream.ReadAsync(buffer, token);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
    {
        return _stream.WriteAsync(buffer, token);
    }

    public override void Flush()
    {
        _stream.Flush();
    }

    public override Task FlushAsync(CancellationToken token)
    {
        return _stream.FlushAsync(token);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

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
            _connection.FinishAsync().GetAwaiter().GetResult();
        }
        catch (IOException) { }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Closes the transfer and waits for the server to say it is over, which is a round trip on the control
    /// connection rather than the formality closing a file is.
    /// </summary>
    /// <returns>A task that finishes once the server has answered.</returns>
    public override async ValueTask DisposeAsync()
    {
        if (_closed)
        {
            await base.DisposeAsync().ConfigureAwait(false);

            return;
        }

        _closed = true;

        await _stream.DisposeAsync().ConfigureAwait(false);
        _data.Dispose();

        try
        {
            await _connection.FinishAsync().ConfigureAwait(false);
        }
        catch (IOException) { }

        await base.DisposeAsync().ConfigureAwait(false);
    }
}
