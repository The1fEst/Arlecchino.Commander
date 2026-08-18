using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Work;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// The count a progress bar is drawn from while a whole file is carried by one end of a transfer. Every
/// way of reading and writing a stream is counted, since the library decides which one it uses.
/// </summary>
public sealed class CountedStreamTests
{
    [Fact]
    public void ReadingIsCountedWhicheverWayItIsAskedFor()
    {
        var position = 0L;
        using var inner = new MemoryStream(new byte[300]);
        using var total = new CountedStream(inner, bytes => position += bytes);

        Assert.Equal(100, total.Read(new byte[100], 0, 100));
        Assert.Equal(100, total.Read(new byte[100].AsSpan()));
        Assert.Equal(200, position);
    }

    [Fact]
    public async Task ReadingAsynchronouslyIsCountedToo()
    {
        var position = 0L;
        using var inner = new MemoryStream(new byte[300]);
        await using var total = new CountedStream(inner, bytes => position += bytes);

        Assert.Equal(100, await total.ReadAsync(new byte[100], 0, 100, CancellationToken.None));
        Assert.Equal(100, await total.ReadAsync(new byte[100].AsMemory(), CancellationToken.None));
        Assert.Equal(200, position);
    }

    [Fact]
    public async Task WritingIsCountedWhicheverWayItIsAskedFor()
    {
        var position = 0L;
        using var inner = new MemoryStream();
        await using var total = new CountedStream(inner, bytes => position += bytes);

        total.Write(new byte[10], 0, 10);
        total.Write(new byte[10].AsSpan());
        await total.WriteAsync(new byte[10], 0, 10, CancellationToken.None);
        await total.WriteAsync(new byte[10].AsMemory(), CancellationToken.None);

        Assert.Equal(40, position);
        Assert.Equal(40, inner.Length);
    }

    /// <summary>
    /// Reading past the end counts nothing, so a bar cannot be pushed along by a stream that has
    /// nothing left to give.
    /// </summary>
    [Fact]
    public void TheEndOfAStreamCountsNothing()
    {
        var position = 0L;
        using var inner = new MemoryStream(new byte[10]);
        using var total = new CountedStream(inner, bytes => position += bytes);

        Assert.Equal(10, total.Read(new byte[100], 0, 100));
        Assert.Equal(0, total.Read(new byte[100], 0, 100));
        Assert.Equal(10, position);
    }

    /// <summary>What was wrapped stays open, because whoever opened it is the one that closes it.</summary>
    [Fact]
    public void ClosingTheCountLeavesTheStreamAlone()
    {
        using var inner = new MemoryStream(new byte[10]);

        using (var total = new CountedStream(inner, static _ => { }))
        {
            Assert.Equal(4, total.Read(new byte[4], 0, 4));
        }

        Assert.Equal(6, inner.Length - inner.Position);
    }
}
