using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// Sending a whole file to a server over several handles at once. What is checked is the bytes that
/// landed, since a piece written at the wrong offset still comes back as a file of the right length.
/// </summary>
public sealed class SftpUploadTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(512 * 1024)]
    [InlineData((512 * 1024) + 1)]
    [InlineData(512 * 1024 * 9)]
    [InlineData((512 * 1024 * 9) + 12345)]
    public async Task WhatArrivesIsWhatWasSent(int length)
    {
        var upload = Bytes(length);
        var server = new Server();

        await SftpUpload.SendAsync(server.OpenAsync, new MemoryStream(upload), CancellationToken.None);

        Assert.Equal(upload, server.Written());
    }

    /// <summary>
    /// A source that answers with less than was asked for, which is what a stream coming off another
    /// server does. A reader that took a short read for the end of the file would send a truncated one.
    /// </summary>
    [Fact]
    public async Task ASourceThatDribblesStillArrivesWhole()
    {
        var upload = Bytes((512 * 1024 * 3) + 77);
        var server = new Server();

        await SftpUpload.SendAsync(server.OpenAsync, new Dribbling(upload), CancellationToken.None);

        Assert.Equal(upload, server.Written());
    }

    [Fact]
    public async Task TheFirstHandleTruncatesAndTheRestDoNot()
    {
        var server = new Server();

        await SftpUpload.SendAsync(server.OpenAsync, new MemoryStream(Bytes(1024)), CancellationToken.None);

        Assert.Equal(FileMode.Create, server.Modes[0]);
        Assert.All(server.Modes.GetRange(1, server.Modes.Count - 1), mode => Assert.Equal(FileMode.OpenOrCreate, mode));
    }

    [Fact]
    public async Task SeveralHandlesShareTheWork()
    {
        var server = new Server();

        await SftpUpload.SendAsync(server.OpenAsync, new MemoryStream(Bytes(512 * 1024 * 8)), CancellationToken.None);

        Assert.True(server.Modes.Count > 1);
    }

    /// <summary>A handle that refuses stops the transfer rather than leaving the reader waiting.</summary>
    [Fact]
    public async Task AHandleThatFailsEndsIt()
    {
        var server = new Server { FailAt = 2 };

        await Assert.ThrowsAnyAsync<IOException>(() =>
            SftpUpload.SendAsync(server.OpenAsync, new MemoryStream(Bytes(512 * 1024 * 12)), CancellationToken.None));
    }

    private static byte[] Bytes(int length)
    {
        var bytes = new byte[length];

        for (var i = 0; i < length; i++)
        {
            bytes[i] = (byte)((i * 31) + (i / 1024));
        }

        return bytes;
    }

    /// <summary>
    /// What the far end amounts to here: one file that several handles write into at their own offsets.
    /// </summary>
    private sealed class Server
    {
        private readonly Lock _gate = new();
        private byte[] _file = [];
        private int _count;

        public List<FileMode> Modes { get; } = [];

        public int FailAt { get; init; }

        public Task<Stream> OpenAsync(FileMode mode, CancellationToken token)
        {
            lock (_gate)
            {
                Modes.Add(mode);

                if (mode == FileMode.Create)
                {
                    _file = [];
                }
            }

            return Task.FromResult<Stream>(new Handle(this));
        }

        public byte[] Written()
        {
            lock (_gate)
            {
                return _file;
            }
        }

        private void Put(long offset, byte[] buffer, int start, int count)
        {
            lock (_gate)
            {
                if (FailAt > 0 && ++_count > FailAt)
                {
                    throw new IOException("the server gave up");
                }

                if (_file.Length < offset + count)
                {
                    Array.Resize(ref _file, (int)offset + count);
                }

                Array.Copy(buffer, start, _file, offset, count);
            }
        }

        /// <summary>One open handle: it remembers where it is and writes there.</summary>
        /// <param name="server">The file every handle is writing into.</param>
        private sealed class Handle(Server server) : Stream
        {
            public override bool CanRead => false;

            public override bool CanSeek => true;

            public override bool CanWrite => true;

            public override long Length => server.Written().Length;

            public override long Position { get; set; }

            public override void Flush() { }

            public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin origin)
            {
                Position = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => Position + offset,
                    _ => Length + offset,
                };

                return Position;
            }

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count)
            {
                server.Put(Position, buffer, offset, count);
                Position += count;
            }

            public override async ValueTask WriteAsync(
                ReadOnlyMemory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                await Task.Yield();

                Write(buffer.ToArray(), 0, buffer.Length);
            }
        }
    }

    /// <summary>A source that never answers with everything it was asked for.</summary>
    /// <param name="bytes">What it hands out, a little at a time.</param>
    private sealed class Dribbling(byte[] bytes) : Stream
    {
        private int _position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => bytes.Length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var outcome = Math.Min(Math.Min(count, 8191), bytes.Length - _position);

            Array.Copy(bytes, _position, buffer, offset, outcome);
            _position += outcome;

            return outcome;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            var outcome = Math.Min(Math.Min(buffer.Length, 8191), bytes.Length - _position);

            bytes.AsMemory(_position, outcome).CopyTo(buffer);
            _position += outcome;

            return outcome;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
