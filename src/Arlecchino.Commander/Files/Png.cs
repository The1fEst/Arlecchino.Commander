using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using Arlecchino.Rendering;

namespace Arlecchino.Commander.Files;

/// <summary>What a PNG turned out to hold, ready to be handed to a picture.</summary>
/// <param name="Pixels">The pixels, row by row from the top left.</param>
/// <param name="Width">How wide it is.</param>
/// <param name="Height">How tall it is.</param>
public sealed record Raster(Rgb[] Pixels, int Width, int Height);

/// <summary>
/// Reads a PNG into pixels. Arlecchino draws pixels rather than files, so somebody has to do this,
/// and a file manager opens whatever it is pointed at — so nothing here throws. Anything that is not
/// a PNG this can read comes back as <c>null</c> and is shown as bytes instead, which is what the
/// viewer did with every binary before.
///
/// Eight bits a channel and no interlacing, which is what all but a handful of PNGs are. Grey,
/// palette, truecolour and either of those with alpha are all read; alpha itself is dropped, since a
/// terminal has nothing to show it against.
/// </summary>
public static class Png
{
    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Whether the bytes begin the way a PNG does.</summary>
    /// <param name="bytes">The head of a file.</param>
    /// <returns><c>true</c> when it is worth trying to read.</returns>
    public static bool Starts(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < Signature.Length)
        {
            return false;
        }

        return bytes[..Signature.Length].SequenceEqual(Signature);
    }

    /// <summary>Reads the picture.</summary>
    /// <param name="bytes">The whole file.</param>
    /// <returns>The pixels, or <c>null</c> when this is not a PNG that can be read.</returns>
    public static Raster? Read(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        try
        {
            return Decode(bytes);
        }
        catch (Exception failure) when (failure is InvalidDataException or IOException or
                                            ArgumentException or IndexOutOfRangeException or
                                            OverflowException or OutOfMemoryException)
        {
            return null;
        }
    }

    private static Raster? Decode(byte[] bytes)
    {
        if (!Starts(bytes))
        {
            return null;
        }

        using var deflated = new MemoryStream();

        var at = Signature.Length;
        var width = 0;
        var height = 0;
        var colour = -1;
        byte[]? palette = null;

        while (at + 8 <= bytes.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(at, 4));

            if (length < 0 || at + 12 + length > bytes.Length)
            {
                break;
            }

            var kind = Encoding.ASCII.GetString(bytes, at + 4, 4);
            var chunk = bytes.AsSpan(at + 8, length);

            at += 12 + length;

            switch (kind)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(chunk[..4]);
                    height = BinaryPrimitives.ReadInt32BigEndian(chunk.Slice(4, 4));
                    colour = chunk[9];

                    if (chunk[8] != 8 || chunk[12] != 0)
                    {
                        return null;
                    }

                    break;

                case "PLTE":
                    palette = chunk.ToArray();
                    break;

                case "IDAT":
                    deflated.Write(chunk);
                    break;

                case "IEND":
                    at = bytes.Length;
                    break;
            }
        }

        var channels = Channels(colour);

        if (width <= 0 || height <= 0 || channels == 0 || deflated.Length == 0)
        {
            return null;
        }

        if (colour == 3 && palette is null)
        {
            return null;
        }

        deflated.Position = 0;

        return Unfilter(deflated, width, height, channels, colour, palette);
    }

    /// <summary>How many bytes one pixel takes before the filtering is undone.</summary>
    /// <param name="colour">The PNG colour type.</param>
    /// <returns>The count, or nought when the type is not one that is read.</returns>
    private static int Channels(int colour) => colour switch
    {
        0 => 1,
        2 => 3,
        3 => 1,
        4 => 2,
        6 => 4,
        _ => 0,
    };

    private static Raster? Unfilter(
        Stream deflated,
        int width,
        int height,
        int channels,
        int colour,
        byte[]? palette)
    {
        using var inflate = new ZLibStream(deflated, CompressionMode.Decompress);

        var stride = width * channels;
        var raw = new byte[(stride + 1) * height];
        var read = 0;

        while (read < raw.Length)
        {
            var got = inflate.Read(raw, read, raw.Length - read);

            if (got == 0)
            {
                return null;
            }

            read += got;
        }

        var pixels = new Rgb[width * height];
        var line = new byte[stride];
        var above = new byte[stride];

        for (var row = 0; row < height; row++)
        {
            var filter = raw[row * (stride + 1)];

            Array.Copy(raw, (row * (stride + 1)) + 1, line, 0, stride);

            for (var index = 0; index < stride; index++)
            {
                var left = index >= channels ? line[index - channels] : 0;
                var corner = index >= channels ? above[index - channels] : 0;

                line[index] += filter switch
                {
                    1 => (byte)left,
                    2 => above[index],
                    3 => (byte)((left + above[index]) / 2),
                    4 => Paeth(left, above[index], corner),
                    _ => 0,
                };
            }

            for (var column = 0; column < width; column++)
            {
                if (!Pixel(line, column, channels, colour, palette, out var pixel))
                {
                    return null;
                }

                pixels[(row * width) + column] = pixel;
            }

            Array.Copy(line, above, stride);
        }

        return new(pixels, width, height);
    }

    private static bool Pixel(
        byte[] line,
        int column,
        int channels,
        int colour,
        byte[]? palette,
        out Rgb pixel)
    {
        var at = column * channels;

        if (colour == 3)
        {
            var entry = line[at] * 3;

            if (palette is null || entry + 2 >= palette.Length)
            {
                pixel = default;

                return false;
            }

            pixel = new(palette[entry], palette[entry + 1], palette[entry + 2]);

            return true;
        }

        pixel = colour is 0 or 4
            ? new(line[at], line[at], line[at])
            : new(line[at], line[at + 1], line[at + 2]);

        return true;
    }

    private static byte Paeth(int left, int above, int corner)
    {
        var guess = left + above - corner;
        var toLeft = Math.Abs(guess - left);
        var toAbove = Math.Abs(guess - above);
        var toCorner = Math.Abs(guess - corner);

        return (byte)(toLeft <= toAbove && toLeft <= toCorner ? left : toAbove <= toCorner ? above : corner);
    }
}
