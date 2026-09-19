using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Pictures;
using Arlecchino.Widgets.Pictures;

namespace Arlecchino.Commander.Views.Viewing;

/// <summary>
/// The pictures a document names, read through the source the document itself came from, a folder on a
/// server being as much a folder as one on a disk. A picture asked for is read for the frames after it.
/// </summary>
/// <param name="source">Where the document came from.</param>
/// <param name="path">Which document, so that what it names is looked for beside it.</param>
internal sealed class Illustrations(IFileSource source, string path)
{
    /// <summary>How many pictures of one document are held at once.</summary>
    private const int MostPictures = 48;

    /// <summary>How much of a file is read before it is given up as too large to be a picture here.</summary>
    private const int MostBytes = 8 * 1024 * 1024;

    /// <summary>
    /// How many pixels a picture is worth holding. A document draws its pictures a dozen rows tall, which
    /// is a fraction of what a terminal would show one at, and the rest would be held and never drawn.
    /// </summary>
    private const int Detail = 128 * 1024;

    private readonly Dictionary<string, Picture?> _pictures = new(StringComparer.Ordinal);

    /// <summary>
    /// The picture at an address, once it is there. It is asked on every frame, so the answer comes from
    /// what has been read rather than from reading; the first asking starts the read.
    /// </summary>
    /// <param name="address">Where the document says the picture is.</param>
    /// <returns>The picture, or <c>null</c> until there is one, or for good where there will not be.</returns>
    public Picture? Of(string address)
    {
        if (_pictures.TryGetValue(address, out var picture))
        {
            return picture;
        }

        _pictures[address] = null;

        if (_pictures.Count <= MostPictures && Near(address))
        {
            FrameThread.Post(async () => _pictures[address] = await ReadAsync(address).ConfigureAwait(true));
        }

        return null;
    }

    /// <summary>
    /// Whether a picture is one this can go and get. Fetching a picture from the web would tell whoever
    /// wrote the document that the file had been opened.
    /// </summary>
    /// <param name="address">Where the document says the picture is.</param>
    /// <returns><c>true</c> when it is a file rather than somewhere to go.</returns>
    private static bool Near(string address) =>
        address.Length > 0 &&
        !address.StartsWith("//", StringComparison.Ordinal) &&
        !address.Contains("://", StringComparison.Ordinal) &&
        !address.StartsWith("data:", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads a picture, answering nothing for a file that is not one or cannot be read. It is decoded
    /// away from the frame, a document of eighty charts costing a second of them otherwise.
    /// </summary>
    /// <param name="address">Where the document says it is.</param>
    /// <returns>The picture.</returns>
    private async Task<Picture?> ReadAsync(string address)
    {
        try
        {
            await using var stream = await source.OpenReadAsync(Beside(address), CancellationToken.None)
                .ConfigureAwait(true);
            var bytes = await Reading.TakeAsync(stream, new(), MostBytes).ConfigureAwait(true);
            var raster = await Task.Run(() => Decoded(bytes)).ConfigureAwait(true);

            if (raster is null)
            {
                return null;
            }

            var picture = new Picture { Detail = Detail };

            picture.Show(raster.Pixels, raster.Width, raster.Height);

            return picture;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>What a file turns out to hold, at the size a document draws a picture.</summary>
    /// <param name="bytes">The file.</param>
    /// <returns>The pixels, or nothing where the file is no picture this can read.</returns>
    private static Raster? Decoded(byte[] bytes) =>
        PictureFormats.For(bytes) is { } format ? format.Read(bytes, PictureLimits.For(Detail)) : null;

    /// <summary>
    /// Where a picture is, worked out from where the document is. The parts are walked through the source
    /// itself, a path being spelled by whatever holds the file.
    /// </summary>
    /// <param name="address">What the document said.</param>
    /// <returns>The path to open.</returns>
    private string Beside(string address)
    {
        if (address.StartsWith('/'))
        {
            return address;
        }

        var at = source.Parent(path) ?? "";

        foreach (var part in address.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            at = part switch
            {
                "." => at,
                ".." => source.Parent(at) ?? at,
                _ => source.Combine(at, part),
            };
        }

        return at;
    }
}
