using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Views.Viewing;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Commands;
using Arlecchino.Focus;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Layout;
using Arlecchino.Navigation;
using Arlecchino.Rendering;
using Arlecchino.Widgets;
using Arlecchino.Widgets.Readouts;
using Microsoft.Extensions.Hosting;
using static Arlecchino.Layout.PaneSplit;
using static Arlecchino.Layout.PaneTree;

namespace Arlecchino.Commander.Views;

/// <summary>
/// One file, shown as whatever it turns out to be: a picture, its text, or a hex dump. It is read by
/// <see cref="Reading"/> and framed here, in the bands every other screen wears.
/// </summary>
public sealed class ViewerView : IArlecchinoView
{
    private readonly Surface _surface;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IFileSource _source;
    private readonly string _path;

    private PaneTree _layout;
    private FocusRing _focus;

    private string _kind = Loc(LocString.ViewerReading);
    private long _read;

    /// <summary>
    /// Opens the viewer. The file is not read here: what is built is the chrome with an empty body, which
    /// fills in when the bytes arrive.
    /// </summary>
    /// <param name="surface">Where it draws.</param>
    /// <param name="sessions">Says which file is being viewed and where it lives.</param>
    /// <param name="options">Supplies the keymap.</param>
    /// <param name="lifetime">Stops the application on F10.</param>
    public ViewerView(Surface surface, Sessions sessions, ArlecchinoOptions options, IHostApplicationLifetime lifetime)
    {
        _surface = surface;
        _lifetime = lifetime;
        _path = sessions.Viewing.Value;
        _source = sessions.ViewingSource;

        var size = sessions.ViewingSize;

        _layout = Chrome(new TextView(options.Keymap) { Text = "" });
        _focus = _layout.AsFocusRing(options.Keymap);

        _ = Opening();

        async Task Opening()
        {
            var (body, kind, read) = await Reading.OpenAsync(_source, _path, size, options).ConfigureAwait(false);

            FrameThread.Post(() =>
            {
                _kind = kind;
                _read = read;
                _layout = Chrome(body);
                _focus = _layout.AsFocusRing(options.Keymap);
            });
        }
    }

    /// <inheritdoc/>
    public void Draw() => _layout.Draw(Sheet.Inside(_surface.Content));

    /// <inheritdoc/>
    public ViewRoute Handle(KeyPress key) => _focus.Handle(key);

    /// <inheritdoc/>
    public ViewRoute HandleMouse(MouseEvent mouse) => _focus.HandleMouse(mouse);

    /// <inheritdoc/>
    public IReadOnlyList<ViewCommand> Commands() =>
    [
        Bind.Going(new(ConsoleKey.Escape), LocString.KeyBack, static () => ViewKind.Commander),
        Bind.Going(new(ConsoleKey.F3), LocString.KeyBack, static () => ViewKind.Commander),
        Bind.To(new(ConsoleKey.F10), LocString.BarQuit, _lifetime.StopApplication),
    ];

    private PaneTree Chrome(IArlecchinoWidget body) => Branch(
        Rows,
        Sheet.Head,
        Leaf(DrawHeader),
        Branch(Rows, PaneSize.CellsFromEnd(Sheet.Foot), Leaf(body), Leaf(DrawFooter)));

    private void DrawHeader(SurfaceRegion header) => Sheet.Title(
        header,
        _source.IsRemote ? RemotePaths.NameOf(_path) : Path.GetFileName(_path),
        Loc(LocString.ViewerBytes, Sizes.Grouped(_read), _kind));

    /// <summary>
    /// The band along the bottom: which folder the file was opened from, and the keys that leave or
    /// scroll. The name itself is in the band at the top, so what is left to say is where it came from.
    /// </summary>
    /// <param name="footer">The rows to draw on.</param>
    private void DrawFooter(SurfaceRegion footer) => Sheet.Hints(
        footer,
        Paths.Shortened(_source, Folder(), footer.Width / 2),
        Loc(LocString.Joined, Loc(LocString.EscBack), Loc(LocString.ViewerScroll)));

    private string Folder() => _source.IsRemote
        ? RemotePaths.Parent(_path) ?? RemotePaths.Root
        : Path.GetDirectoryName(_path) ?? "";
}
