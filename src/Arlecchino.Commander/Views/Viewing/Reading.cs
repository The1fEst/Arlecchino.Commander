using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;
using Arlecchino.Hosting;
using Arlecchino.Pictures;
using Arlecchino.Rendering.Terminals;
using Arlecchino.Rendering.Text;
using Arlecchino.Widgets;
using Arlecchino.Widgets.Pictures;
using Arlecchino.Widgets.Readouts;

namespace Arlecchino.Commander.Views.Viewing;

/// <summary>
/// What the viewer puts on screen for a file, and what to call it. A picture is shown as itself, anything
/// else as text or as a hex dump, and none of that depends on where the file lives.
/// </summary>
public static class Reading
{
    private const int ReadLimit = 512 * 1024;
    private const int ImageLimit = 32 * 1024 * 1024;
    private const int ChunkBytes = 8192;
    private const int BytesPerRow = 16;
    private const int TextProbe = 8192;

    /// <summary>
    /// Reads the file and decides what to show it with. A picture is read whole, since half of one
    /// decodes to nothing.
    /// </summary>
    /// <param name="source">Where the file lives.</param>
    /// <param name="path">Which file.</param>
    /// <param name="size">How large it is said to be.</param>
    /// <param name="options">Supplies the keymap the text viewer scrolls by.</param>
    /// <returns>What to draw, what to call it, and how much of it was read.</returns>
    public static async Task<(IArlecchinoWidget Body, string Kind, long Read)> OpenAsync(
        IFileSource source,
        string path,
        long size,
        ArlecchinoOptions options)
    {
        try
        {
            await using var stream = await source.OpenReadAsync(path, CancellationToken.None)
                .ConfigureAwait(false);
            var held = new MemoryStream();
            var head = await TakeAsync(stream, held, ReadLimit).ConfigureAwait(false);

            if (PictureFormats.For(head) is { } format)
            {
                var whole = head.Length >= size
                    ? head
                    : await TakeAsync(stream, held, ImageLimit).ConfigureAwait(false);
                var picture = new Picture();

                if (format.Read(whole, PictureLimits.For(picture.Detail)) is { } raster)
                {
                    picture.Show(raster.Pixels, raster.Width, raster.Height);

                    return (
                        picture,
                        Loc(
                            LocString.ViewerPicture,
                            format.Name,
                            raster.Width,
                            raster.Height,
                            Drawing(picture)),
                        whole.Length);
                }
            }

            var binary = IsBinary(head);
            var text = new TextView(options.Keymap)
            {
                Text = binary
                    ? Dump(head)
                    : Encoding.UTF8.GetString(head).Replace("\t", "    ", StringComparison.Ordinal),
            };

            return (text, Truncated(Loc(binary ? LocString.ViewerHex : LocString.ViewerText), head.Length, size), size);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (new TextView(options.Keymap) { Text = error.Message }, Loc(LocString.ViewerUnreadable), 0);
        }
    }

    /// <summary>
    /// How the picture will reach the terminal: the pixels themselves where the terminal admitted to a
    /// graphics protocol, and half-blocks where it did not.
    /// </summary>
    /// <param name="picture">The picture about to be drawn.</param>
    /// <returns>What to call the way it is drawn.</returns>
    private static string Drawing(Picture picture) =>
        TerminalCapabilities.Resolve(picture.Protocol ?? Glyphs.Picture)
            .ToString()
            .ToLowerInvariant();

    private static string Truncated(string kind, int read, long size) =>
        read < size ? Loc(LocString.ViewerFirst, kind, Sizes.Brief(read)) : kind;

    /// <summary>
    /// Reads the file up to a limit, carrying on from where the last read left off. Over a network,
    /// reading the front of it a second time would be the file twice.
    /// </summary>
    /// <param name="stream">The file, still open.</param>
    /// <param name="held">What has been read so far, which this adds to.</param>
    /// <param name="limit">How much of the file to have read by the end.</param>
    /// <returns>The bytes.</returns>
    private static async Task<byte[]> TakeAsync(Stream stream, MemoryStream held, long limit)
    {
        var chunk = new byte[ChunkBytes];

        while (held.Length < limit)
        {
            var wanted = (int)Math.Min(chunk.Length, limit - held.Length);
            var read = await stream.ReadAsync(chunk.AsMemory(0, wanted), CancellationToken.None)
                .ConfigureAwait(false);

            if (read <= 0)
            {
                break;
            }

            await held.WriteAsync(chunk.AsMemory(0, read), CancellationToken.None).ConfigureAwait(false);
        }

        return held.ToArray();
    }

    private static bool IsBinary(byte[] bytes)
    {
        var probed = Math.Min(TextProbe, bytes.Length);

        for (var index = 0; index < probed; index++)
        {
            if (bytes[index] == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string Dump(byte[] bytes)
    {
        var text = new StringBuilder();

        for (var offset = 0; offset < bytes.Length; offset += BytesPerRow)
        {
            var row = bytes.AsSpan(offset, Math.Min(BytesPerRow, bytes.Length - offset));

            text.Append(offset.ToString("x8", CultureInfo.InvariantCulture)).Append("  ");

            for (var index = 0; index < BytesPerRow; index++)
            {
                text.Append(index < row.Length
                        ? row[index].ToString("x2", CultureInfo.InvariantCulture)
                        : "  ")
                    .Append(' ');
            }

            text.Append(' ');

            foreach (var value in row)
            {
                text.Append(value is >= 32 and < 127 ? (char)value : '.');
            }

            text.AppendLine();
        }

        return text.ToString();
    }
}
