using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Files.Work;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Commands;
using Arlecchino.Focus;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Layout;
using Arlecchino.Navigation;
using Arlecchino.Rendering;
using Arlecchino.Widgets;
using Arlecchino.Widgets.Pictures;
using Arlecchino.Widgets.Readouts;
using Microsoft.Extensions.Hosting;
using static Arlecchino.Layout.PaneSplit;
using static Arlecchino.Layout.PaneTree;

namespace Arlecchino.Commander.Views;

public sealed class ViewerView : IArlecchinoView
{
    private const int ReadLimit = 512 * 1024;
    private const int ImageLimit = 32 * 1024 * 1024;
    private const int ChunkBytes = 8192;
    private const int BytesPerRow = 16;
    private const int TextProbe = 8192;

    private readonly Surface _surface;
    private readonly IHostApplicationLifetime _lifetime;

    private PaneTree _layout;
    private FocusRing _focus;

    private string _kind = Loc(LocString.ViewerReading);
    private long _read;

    /// <summary>
    /// Opens the viewer. The file is not read here: reading it is a wait, and a view that waits in its
    /// constructor is a frame that does not appear. What is built is the chrome with an empty body,
    /// which fills in when the bytes arrive — the same shape a panel over a server has always had.
    /// </summary>
    /// <param name="surface">Where it draws.</param>
    /// <param name="sessions">Says which file is being viewed and where it lives.</param>
    /// <param name="options">Supplies the keymap.</param>
    /// <param name="lifetime">Stops the application on F10.</param>
    public ViewerView(Surface surface, Sessions sessions, ArlecchinoOptions options, IHostApplicationLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(options);

        _surface = surface;
        _lifetime = lifetime;

        var path = sessions.Viewing.Value;
        var size = sessions.ViewingSize;
        var source = sessions.ViewingSource;

        var empty = new TextView(options.Keymap) { Text = "" };

        var status = new StatusBar
        {
            Left = [() => Loc(LocString.ViewerBytes, Sizes.Grouped(_read), _kind)],
            Right = [static () => Loc(LocString.ViewerBack), static () => Loc(LocString.ViewerScroll)],
        };

        _layout = Chrome(empty);
        _focus = _layout.AsFocusRing(options.Keymap);

        _ = Opening();

        async Task Opening()
        {
            var (body, kind, read) = await LoadAsync(source, path, size, options).ConfigureAwait(false);

            FrameThread.Post(() =>
            {
                _kind = kind;
                _read = read;
                _layout = Chrome(body);
                _focus = _layout.AsFocusRing(options.Keymap);
            });
        }

        PaneTree Chrome(IArlecchinoWidget body) => Branch(
            Rows,
            PaneSize.CellsFromEnd(1),
            Leaf(body, () => Path.GetFileName(path)),
            Leaf(status));
    }

    public void Draw() => _layout.Draw(_surface.Content);

    public ViewRoute Handle(ConsoleKeyInfo key) => _focus.Handle(key);

    public ViewRoute HandleMouse(MouseEvent mouse) => _focus.HandleMouse(mouse);

    public IReadOnlyList<ViewCommand> Commands() =>
    [
        Bind.Going(new(ConsoleKey.Escape), LocString.KeyBack, static () => ViewKind.Commander),
        Bind.Going(new(ConsoleKey.F3), LocString.KeyBack, static () => ViewKind.Commander),
        Bind.To(new(ConsoleKey.F10), LocString.BarQuit, _lifetime.StopApplication),
    ];

    /// <summary>
    /// Reads the file and decides what to show it with. A PNG is drawn as itself; anything else is text or a
    /// hex dump, as it always was. A picture has to be read whole, since a PNG is one deflate stream from end
    /// to end and the first half of one decodes to nothing. That is why its limit is its own, and larger than
    /// the one the text viewer reads under.
    /// </summary>
    /// <param name="source">Where the file lives.</param>
    /// <param name="path">Which file.</param>
    /// <param name="size">How large it is said to be.</param>
    /// <param name="options">Supplies the keymap the text viewer scrolls by.</param>
    /// <returns>What to draw, what to call it, and how much of it was read.</returns>
    private static async Task<(IArlecchinoWidget Body, string Kind, long Read)> LoadAsync(
        IFileSource source,
        string path,
        long size,
        ArlecchinoOptions options)
    {
        try
        {
            var head = await HeadAsync(source, path, ReadLimit).ConfigureAwait(false);

            if (Png.Starts(head))
            {
                var whole = head.Length >= size
                    ? head
                    : await HeadAsync(source, path, ImageLimit).ConfigureAwait(false);

                if (Png.Read(whole) is { } raster)
                {
                    var picture = new Picture();

                    picture.Show(raster.Pixels, raster.Width, raster.Height);

                    return (picture, Loc(LocString.ViewerPicture, raster.Width, raster.Height), whole.Length);
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

    private static string Truncated(string kind, int read, long size) =>
        read < size ? Loc(LocString.ViewerFirst, kind, Sizes.Brief(read)) : kind;

    /// <summary>
    /// The front of a file, as much of it as the viewer will show. The whole of a large file is never read, so
    /// this waits on a few blocks rather than on the file. But it does wait, which is why the view is built
    /// from what this hands back rather than around a stream it reads while drawing.
    /// </summary>
    /// <param name="source">Where the file is.</param>
    /// <param name="path">The file.</param>
    /// <param name="limit">How much of it to read.</param>
    /// <returns>The bytes.</returns>
    private static async Task<byte[]> HeadAsync(IFileSource source, string path, long limit)
    {
        await using var stream = await source.OpenReadAsync(path, CancellationToken.None).ConfigureAwait(false);
        var held = new MemoryStream();
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
