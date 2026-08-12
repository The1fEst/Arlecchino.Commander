using System;
using System.Buffers;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Renci.SshNet;

namespace Arlecchino.Commander.Files.Sources;

/// <summary>
/// Writes a whole file to a server over several handles at once, all of them on the one session the caller
/// leased. One reader hands out the bytes in order, and each handle writes its piece at its own offset.
/// </summary>
internal static class SftpUpload
{
    /// <summary>
    /// How much of the file one handle claims at a time, which only has to be well past the 32 KB a server
    /// takes at once. A handle then still has writes going when it asks for more.
    /// </summary>
    private const int Chunk = 512 * 1024;

    /// <summary>
    /// How many handles write at once, and so how many times faster than one this goes. Every handle in
    /// flight holds a piece of the file in memory.
    /// </summary>
    private const int Handles = 4;

    /// <summary>Writes a whole file, reading it in order and writing it in pieces at once.</summary>
    /// <param name="client">The leased session; every handle is opened on it.</param>
    /// <param name="reading">Where the bytes come from, read to its end.</param>
    /// <param name="target">The path to write.</param>
    /// <param name="token">Gives up the transfer.</param>
    /// <returns>A task that finishes when the whole file has been written.</returns>
    public static Task SendAsync(
        SftpClient client,
        Stream reading,
        string target,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(client);

        return SendAsync(
            async (mode, waiting) =>
                await client.OpenAsync(target, mode, FileAccess.Write, waiting).ConfigureAwait(false),
            reading,
            token);
    }

    /// <summary>
    /// The same, told where to get its handles rather than how, so a test can answer with its own and read
    /// back what was assembled. A piece landing at the wrong offset is what would go unnoticed otherwise.
    /// </summary>
    /// <param name="opening">Opens one handle; told whether it is the one that truncates.</param>
    /// <param name="reading">Where the bytes come from, read to its end.</param>
    /// <param name="token">Gives up the transfer.</param>
    /// <returns>A task that finishes when the whole file has been written.</returns>
    internal static async Task SendAsync(
        Func<FileMode, CancellationToken, Task<Stream>> opening,
        Stream reading,
        CancellationToken token)
    {
        using var failed = CancellationTokenSource.CreateLinkedTokenSource(token);

        var pieces = Channel.CreateBounded<Piece>(new BoundedChannelOptions(Handles)
        {
            SingleWriter = true,
        });

        var handles = await OpenAsync(opening, token).ConfigureAwait(false);

        try
        {
            var running = new Task[handles.Length + 1];

            for (var i = 0; i < handles.Length; i++)
            {
                running[i] = WriteAsync(handles[i], pieces.Reader, failed);
            }

            running[^1] = ReadAsync(reading, pieces.Writer, failed.Token);

            var all = Task.WhenAll(running);

            try
            {
                await all.ConfigureAwait(false);
            }
            catch (Exception error)
            {
                ExceptionDispatchInfo.Capture(Blame(all, error)).Throw();

                throw;
            }

            foreach (var handle in handles)
            {
                await handle.FlushAsync(token).ConfigureAwait(false);
            }
        }
        finally
        {
            foreach (var handle in handles)
            {
                await handle.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Opens the handles. The first truncates whatever was there, so it is opened before any of the
    /// others and before a single byte is written; the rest join the file it left.
    /// </summary>
    /// <param name="opening">Opens one handle.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns>The handles, all on the one session.</returns>
    private static async Task<Stream[]> OpenAsync(
        Func<FileMode, CancellationToken, Task<Stream>> opening,
        CancellationToken token)
    {
        var handles = new Stream[Handles];
        var opened = 0;

        try
        {
            for (; opened < handles.Length; opened++)
            {
                handles[opened] = await opening(opened == 0 ? FileMode.Create : FileMode.OpenOrCreate, token)
                    .ConfigureAwait(false);
            }
        }
        catch
        {
            for (var i = 0; i < opened; i++)
            {
                await handles[i].DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }

        return handles;
    }

    /// <summary>
    /// Reads the file in order and hands the pieces out. The channel holds only as many pieces as there
    /// are handles, so a slow line stops the reading rather than filling memory with what it cannot send.
    /// </summary>
    /// <param name="reading">Where the bytes come from.</param>
    /// <param name="pieces">Where the pieces go.</param>
    /// <param name="token">Gives up the read, and is given up on when a handle fails.</param>
    /// <returns>A task that finishes when the stream has ended.</returns>
    private static async Task ReadAsync(Stream reading, ChannelWriter<Piece> pieces, CancellationToken token)
    {
        long offset = 0;

        try
        {
            while (true)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(Chunk);
                var filled = 0;

                try
                {
                    while (filled < Chunk)
                    {
                        var read = await reading
                            .ReadAsync(buffer.AsMemory(filled, Chunk - filled), token)
                            .ConfigureAwait(false);

                        if (read == 0)
                        {
                            break;
                        }

                        filled += read;
                    }
                }
                catch
                {
                    ArrayPool<byte>.Shared.Return(buffer);

                    throw;
                }

                if (filled == 0)
                {
                    ArrayPool<byte>.Shared.Return(buffer);

                    break;
                }

                await pieces.WriteAsync(new(offset, buffer, filled), token).ConfigureAwait(false);
                offset += filled;
            }

            pieces.Complete();
        }
        catch (Exception error)
        {
            pieces.Complete(error);

            throw;
        }
    }

    /// <summary>
    /// Takes pieces as they come and writes each where it belongs, giving up on the reader when a handle
    /// fails. The buffer is emptied before the seek, since a seek holding bytes would send them and wait.
    /// </summary>
    /// <param name="handle">This handle.</param>
    /// <param name="pieces">Where the pieces come from.</param>
    /// <param name="failed">Cancelled when any handle gives up.</param>
    /// <returns>A task that finishes when there are no more pieces.</returns>
    private static async Task WriteAsync(
        Stream handle,
        ChannelReader<Piece> pieces,
        CancellationTokenSource failed)
    {
        try
        {
            await foreach (var piece in pieces.ReadAllAsync(failed.Token).ConfigureAwait(false))
            {
                try
                {
                    await handle.FlushAsync(failed.Token).ConfigureAwait(false);

                    handle.Seek(piece.Offset, SeekOrigin.Begin);

                    await handle
                        .WriteAsync(piece.Buffer.AsMemory(0, piece.Count), failed.Token)
                        .ConfigureAwait(false);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(piece.Buffer);
                }
            }
        }
        catch
        {
            await failed.CancelAsync().ConfigureAwait(false);

            throw;
        }
    }

    /// <summary>
    /// Which of the failures to answer with: the complaint from the server rather than the cancellations that
    /// are merely how the rest were stopped. A cancellation would be filed as a transfer called off.
    /// </summary>
    /// <param name="all">The finished work, faulted.</param>
    /// <param name="fallback">What was thrown, for when there is nothing better.</param>
    /// <returns>The failure worth reporting.</returns>
    private static Exception Blame(Task all, Exception fallback)
    {
        if (all.Exception is not { } errors)
        {
            return fallback;
        }

        foreach (var error in errors.InnerExceptions)
        {
            if (error is not OperationCanceledException)
            {
                return error;
            }
        }

        return fallback;
    }

    /// <summary>A piece of the file, and where in it the piece belongs.</summary>
    /// <param name="Offset">Where it goes.</param>
    /// <param name="Buffer">The bytes, rented, and returned once written.</param>
    /// <param name="Count">How many of them are the file.</param>
    private readonly record struct Piece(long Offset, byte[] Buffer, int Count);
}
