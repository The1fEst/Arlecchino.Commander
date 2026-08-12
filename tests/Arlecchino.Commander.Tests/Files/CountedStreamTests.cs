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
        var moved = 0L;
        using var inner = new MemoryStream(new byte[300]);
        using var counted = new CountedStream(inner, bytes => moved += bytes);

        Assert.Equal(100, counted.Read(new byte[100], 0, 100));
        Assert.Equal(100, counted.Read(new byte[100].AsSpan()));
        Assert.Equal(200, moved);
    }

    [Fact]
    public async Task ReadingAsynchronouslyIsCountedToo()
    {
        var moved = 0L;
        using var inner = new MemoryStream(new byte[300]);
        await using var counted = new CountedStream(inner, bytes => moved += bytes);

        Assert.Equal(100, await counted.ReadAsync(new byte[100], 0, 100, CancellationToken.None));
        Assert.Equal(100, await counted.ReadAsync(new byte[100].AsMemory(), CancellationToken.None));
        Assert.Equal(200, moved);
    }

    [Fact]
    public async Task WritingIsCountedWhicheverWayItIsAskedFor()
    {
        var moved = 0L;
        using var inner = new MemoryStream();
        await using var counted = new CountedStream(inner, bytes => moved += bytes);

        counted.Write(new byte[10], 0, 10);
        counted.Write(new byte[10].AsSpan());
        await counted.WriteAsync(new byte[10], 0, 10, CancellationToken.None);
        await counted.WriteAsync(new byte[10].AsMemory(), CancellationToken.None);

        Assert.Equal(40, moved);
        Assert.Equal(40, inner.Length);
    }

    /// <summary>
    /// Reading past the end counts nothing, so a bar cannot be pushed along by a stream that has
    /// nothing left to give.
    /// </summary>
    [Fact]
    public void TheEndOfAStreamCountsNothing()
    {
        var moved = 0L;
        using var inner = new MemoryStream(new byte[10]);
        using var counted = new CountedStream(inner, bytes => moved += bytes);

        Assert.Equal(10, counted.Read(new byte[100], 0, 100));
        Assert.Equal(0, counted.Read(new byte[100], 0, 100));
        Assert.Equal(10, moved);
    }

    /// <summary>What was wrapped stays open, because whoever opened it is the one that closes it.</summary>
    [Fact]
    public void ClosingTheCountLeavesTheStreamAlone()
    {
        using var inner = new MemoryStream(new byte[10]);

        using (var counted = new CountedStream(inner, static _ => { }))
        {
            Assert.Equal(4, counted.Read(new byte[4], 0, 4));
        }

        Assert.Equal(6, inner.Length - inner.Position);
    }
}
