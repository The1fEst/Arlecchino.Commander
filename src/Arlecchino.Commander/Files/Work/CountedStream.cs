using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Arlecchino.Commander.Files.Work;

/// <summary>
/// A stream that says how much has gone through it, and passes everything else straight on.
///
/// A source moving a whole file reports nothing while it does so, and a bar that moves only as each
/// file finishes says nothing at all while a large one is going over. What can still be watched is the
/// stream at the other end: every byte of the file is read out of it or written into it exactly once,
/// so counting there counts the transfer.
///
/// It does not close what it wraps. The stream was opened by whoever asked for the transfer and is
/// closed by them.
/// </summary>
public sealed class CountedStream : Stream
{
    private readonly Stream _inner;
    private readonly Action<long> _moved;

    /// <summary>Wraps a stream in a count.</summary>
    /// <param name="inner">The stream the bytes really go through.</param>
    /// <param name="moved">Told how many bytes went through, as they go.</param>
    public CountedStream(Stream inner, Action<long> moved)
    {
        _inner = inner;
        _moved = moved;
    }

    /// <inheritdoc/>
    public override bool CanRead => _inner.CanRead;

    /// <inheritdoc/>
    public override bool CanSeek => _inner.CanSeek;

    /// <inheritdoc/>
    public override bool CanWrite => _inner.CanWrite;

    /// <inheritdoc/>
    public override long Length => _inner.Length;

    /// <inheritdoc/>
    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    /// <inheritdoc/>
    public override void Flush() => _inner.Flush();

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    /// <inheritdoc/>
    public override void SetLength(long value) => _inner.SetLength(value);

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => Told(_inner.Read(buffer, offset, count));

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer) => Told(_inner.Read(buffer));

    /// <inheritdoc/>
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Told(await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        Told(await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        _inner.Write(buffer, offset, count);
        _moved(count);
    }

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _inner.Write(buffer);
        _moved(buffer.Length);
    }

    /// <inheritdoc/>
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        _moved(count);
    }

    /// <inheritdoc/>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        _moved(buffer.Length);
    }

    private int Told(int read)
    {
        _moved(read);

        return read;
    }
}
