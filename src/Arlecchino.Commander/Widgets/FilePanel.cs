using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files;
using Arlecchino.Commander.Model;
using Arlecchino.Focus;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Navigation;
using Arlecchino.Rendering;
using Arlecchino.Widgets;

namespace Arlecchino.Commander.Widgets;

public sealed class FilePanel : IArlecchinoInteractiveWidget
{
    private const int SizeWidth = 9;
    private const int StampWidth = 11;
    private const int ColumnGap = 2;
    private const int SideRoom = 1;
    private const int MinimumName = 12;
    private const int TitleChrome = 4;
    private const int TitleFloor = 12;
    private const int TitleGuess = 44;
    private const int Chrome = 6;

    private readonly PanelState _state;
    private readonly ArlecchinoKeymap _keymap;
    private readonly KeyText _keys;
    private readonly ListBox<FileEntry> _table;
    private readonly List<FileEntry> _entries = [];

    private SurfaceRegion _heads;
    private string _error = "";
    private string _free = "";
    private string _typed = "";
    private int _width = TitleGuess;
    private bool _loading;
    private bool _searching;

    /// <summary>Creates a panel over one side's state.</summary>
    /// <param name="state">What that side is showing.</param>
    /// <param name="keymap">Keys to obey.</param>
    /// <param name="keys">
    /// Turns a key press into the character it types, so the search that runs while you type and the
    /// marking keys work with a Cyrillic layout switched on.
    /// </param>
    public FilePanel(PanelState state, ArlecchinoKeymap keymap, KeyText keys)
    {
        _state = state;
        _keymap = keymap;
        _keys = keys;

        _table = new(keymap)
        {
            Render = static entry => entry.Name,
            PaintRow = PaintRow,
            OnActivate = Activate,
        };

        Reload();
    }

    public Func<FileEntry, ViewRoute>? OnOpenFile { get; init; }

    /// <summary>
    /// Asked for a shell pattern when <c>+</c> or <c>-</c> is typed. The panel cannot open a box of
    /// its own, so the screen holding it does the asking and calls <see cref="MarkGroup"/> back.
    /// </summary>
    public Action<bool>? OnGroup { get; init; }

    public PanelState State => _state;

    public IFileSource Source => _state.Source;

    public string Folder => _state.Folder;

    public FileEntry? Current => _table.SelectedItem;

    /// <summary>Whether the search that runs while you type is on, in which case typing is its own.</summary>
    public bool IsSearching => _searching;

    /// <summary>What the panel is showing, in the order it is shown.</summary>
    public IReadOnlyList<FileEntry> Entries => _entries;

    public bool IsFocused
    {
        get => _table.IsFocused;
        set => _table.IsFocused = value;
    }

    public IReadOnlyList<FileEntry> Targets()
    {
        if (_state.Marks.Count == 0)
        {
            return Current is { IsParent: false } current ? [current] : [];
        }

        var marked = new List<FileEntry>();

        foreach (var entry in _entries)
        {
            if (!entry.IsParent && _state.Marks.Contains(entry.Name))
            {
                marked.Add(entry);
            }
        }

        return marked;
    }

    /// <summary>
    /// Goes to a folder, once the source has said it is there. Asking is a round trip on a server, so
    /// the panel is left as it is until the answer comes back rather than emptied on the way.
    /// </summary>
    /// <param name="folder">Where to go.</param>
    public void GoTo(string folder)
    {
        _ = Going();

        async Task Going()
        {
            if (!await _state.Source.FolderExistsAsync(folder, CancellationToken.None).ConfigureAwait(false))
            {
                return;
            }

            FrameThread.Post(() =>
            {
                _state.GoTo(folder);
                Reload();
            });
        }
    }

    /// <summary>Goes back to the folder this panel was in before this one.</summary>
    /// <returns><c>false</c> when there is nothing behind it, or what is behind it is gone.</returns>
    public bool Back() => Stepped(_state.Back());

    /// <summary>Goes forward again after <see cref="Back"/>.</summary>
    /// <returns><c>false</c> when it is already at the newest folder, or that folder is gone.</returns>
    public bool Forward() => Stepped(_state.Forward());

    /// <summary>Opens the folder under the cursor, the way Ctrl+PageDown does in Midnight Commander.</summary>
    public void Descend()
    {
        if (Current is { IsFolder: true } current)
        {
            Activate(current);
        }
    }

    /// <summary>Leaves for the folder above, the way Ctrl+PageUp does.</summary>
    public void Ascend() => Up();

    public void Top() => _table.Selected = 0;

    public void Middle() => _table.Selected = _entries.Count / 2;

    public void Bottom() => _table.Selected = Math.Max(0, _entries.Count - 1);

    /// <summary>
    /// Starts the search that runs while you type, which moves the cursor to the first name that
    /// begins with what has been typed so far. Escape, Enter or any other key ends it.
    /// </summary>
    public void Search()
    {
        _searching = true;
        _typed = "";
    }

    /// <summary>Marks, or unmarks, every file whose name fits a shell pattern.</summary>
    /// <param name="pattern">The pattern, as <c>*.cs</c> or <c>a*,b*</c>.</param>
    /// <param name="marking"><c>true</c> to mark what fits, <c>false</c> to unmark it.</param>
    public void MarkGroup(string pattern, bool marking)
    {
        foreach (var entry in _entries)
        {
            if (entry.IsParent || entry.IsFolder || !Glob.Matches(entry.Name, pattern))
            {
                continue;
            }

            if (marking)
            {
                _state.Marks.Add(entry.Name);
            }
            else
            {
                _state.Marks.Remove(entry.Name);
            }
        }
    }

    /// <summary>Marks what is not marked and unmarks what is.</summary>
    public void Invert()
    {
        foreach (var entry in _entries)
        {
            if (entry.IsParent || entry.IsFolder)
            {
                continue;
            }

            if (!_state.Marks.TryAdd(entry.Name))
            {
                _state.Marks.Remove(entry.Name);
            }
        }
    }

    public void Connect(IFileSource source, string folder)
    {
        _state.Connect(source, folder);
        Reload();
    }

    /// <summary>
    /// Reads the folder and lands on it. Every source is read the same way, the disk included: a
    /// folder of a hundred thousand names takes as long as it takes, and the frame that asked for it
    /// carries on drawing meanwhile, saying that it is loading. The alternative — a quick path that
    /// reads a local folder in the middle of composing a frame — is the one that freezes.
    /// </summary>
    public void Reload()
    {
        var cursor = _state.Cursor.Length > 0 ? _state.Cursor : Current?.Name ?? "";
        var source = _state.Source;
        var folder = _state.Folder;
        var hidden = _state.ShowHidden;

        _state.Cursor = "";
        _loading = true;

        _ = Reading();

        async Task Reading()
        {
            var read = await ReadAsync(source, folder, hidden).ConfigureAwait(false);

            FrameThread.Post(() => Landed(source, folder, read, cursor));

            var free = await FreeAsync(source, folder).ConfigureAwait(false);

            FrameThread.Post(() =>
            {
                if (ReferenceEquals(source, _state.Source) && folder == _state.Folder)
                {
                    _free = free;
                }
            });
        }
    }

    public void SortBy(Sorting sorting)
    {
        _state.Descending = _state.Sorting == sorting && !_state.Descending;
        _state.Sorting = sorting;

        Sort();
    }

    /// <summary>
    /// Reads one key while the search is running. Anything that is not a letter to add or a rub-out
    /// ends the search and is left for the panel itself, so a cursor key still moves the cursor.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when the search took it.</returns>
    private bool Typing(ConsoleKeyInfo key)
    {
        if (key.Key is ConsoleKey.Backspace && _typed.Length > 0)
        {
            _typed = _typed[..^1];
            Nearest();

            return true;
        }

        if (key.Modifiers.HasFlag(ConsoleModifiers.Control) || key.Modifiers.HasFlag(ConsoleModifiers.Alt) ||
            _keys.Resolve(key) is not { } typed || char.IsControl(typed))
        {
            _searching = false;

            return key.Key is ConsoleKey.Escape or ConsoleKey.Enter;
        }

        _typed += typed;
        Nearest();

        return true;
    }

    /// <summary>
    /// The three keys Midnight Commander gives to marking by pattern. They are read as characters
    /// rather than bound, because terminals disagree about which key a <c>+</c> came from.
    /// </summary>
    /// <param name="typed">The character that arrived.</param>
    /// <returns><c>true</c> when it was one of them.</returns>
    private bool Grouping(char typed)
    {
        switch (typed)
        {
            case '+':
                OnGroup?.Invoke(true);
                return true;
            case '-':
                OnGroup?.Invoke(false);
                return true;
            case '*':
                Invert();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Moves the cursor to the first name the typed letters begin, keeping it where it is otherwise.</summary>
    private void Nearest()
    {
        for (var index = 0; index < _entries.Count; index++)
        {
            if (!_entries[index].Name.StartsWith(_typed, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _table.Selected = index;

            return;
        }
    }

    private bool Stepped(string? folder)
    {
        if (folder is null)
        {
            return false;
        }

        Reload();

        return true;
    }

    private void Point(string name)
    {
        for (var index = 0; index < _entries.Count; index++)
        {
            if (!string.Equals(_entries[index].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _table.Selected = index;
            return;
        }
    }

    public ViewRoute Activate(FileEntry entry)
    {
        if (!entry.IsFolder)
        {
            return OnOpenFile?.Invoke(entry) ?? ViewRoute.None;
        }

        Enter(entry.Path, entry.IsParent);

        return ViewRoute.None;
    }

    /// <summary>
    /// Draws the panel: where it is looking, what is in it, and what the cursor is on. There is no box
    /// around any of it — the panel is told from the one beside it by the step in the background and by
    /// the rule down its left edge, which is the accent while this is the panel being worked in.
    /// </summary>
    /// <param name="region">The whole panel, rule included.</param>
    /// <returns>An empty region: the panel uses every row it is handed.</returns>
    public SurfaceRegion Draw(SurfaceRegion region)
    {
        if (region.IsEmpty)
        {
            return region;
        }

        var coat = IsFocused ? Skin.Lively : Skin.Quiet;

        region.Fill(coat.Text);

        var (edge, rest) = region.SplitLeft(1);

        edge.Fill(IsFocused ? Focused : coat.Text);

        var body = rest.Inset(new Margin(SideRoom, 0, 2, 0));

        _width = body.Width;

        if (body.IsEmpty)
        {
            return region.Rows(region.Height, 0);
        }

        Where(body.Rows(0, 1), coat);

        if (body.Height <= Chrome)
        {
            return region.Rows(region.Height, 0);
        }

        body.Rows(2, 1).Fill(coat.Rule, '─');

        _heads = body.Rows(3, 1);

        Heads(_heads, coat);

        if (_error.Length > 0)
        {
            body.Rows(4, 1).WriteLine(0, TextWidth.Truncate(_error, body.Width), Skin.Paint(Skin.Crimson, Beneath));
        }
        else
        {
            _table.Draw(body.Rows(4, body.Height - Chrome));
        }

        body.Rows(body.Height - 2, 1).Fill(coat.Rule, '─');
        Foot(body.Rows(body.Height - 1, 1), coat);

        return region.Rows(region.Height, 0);
    }

    public FocusResult Handle(ConsoleKeyInfo key)
    {
        if (_searching && Typing(key))
        {
            return FocusResult.Handled;
        }

        if (_keymap.Mark.Matches(key) || key.Key == ConsoleKey.Insert)
        {
            Mark();
            return FocusResult.Handled;
        }

        if (key.Modifiers == 0 && _keys.Resolve(key) is { } marking && Grouping(marking))
        {
            return FocusResult.Handled;
        }

        return _table.Handle(key);
    }

    /// <summary>
    /// Clicks and the wheel. A click on the column heads sorts by the one that was hit, which is where
    /// anybody who has used a file manager with a mouse will click first; anything lower belongs to the
    /// list.
    /// </summary>
    /// <param name="mouse">The event that arrived.</param>
    /// <returns>What became of it.</returns>
    public FocusResult HandleMouse(MouseEvent mouse)
    {
        if (mouse.Action != MouseAction.Pressed || !_heads.Contains(mouse.Row, mouse.Column) ||
            Hit(_heads.ToLocal(mouse.Row, mouse.Column).Column) is not { } sorting)
        {
            return _table.HandleMouse(mouse);
        }

        SortBy(sorting);

        return FocusResult.Handled;
    }

    /// <summary>Which column head a click landed on.</summary>
    /// <param name="column">How far along the heads the click was.</param>
    /// <returns>What it sorts by, or nothing when it hit the space between them.</returns>
    private Sorting? Hit(int column)
    {
        var (name, size, date) = Widths(_heads.Width);

        if (column < Kinds.TagWidth + name)
        {
            return Sorting.Name;
        }

        if (size > 0 && column < Kinds.TagWidth + name + ColumnGap + size)
        {
            return Sorting.Size;
        }

        return date > 0 ? Sorting.Modified : null;
    }

    /// <summary>
    /// How much room is left, asked after the listing rather than beside it. Two questions at once
    /// would take two sessions of a pool that has a few, and the names are what the frame is waiting on.
    /// </summary>
    /// <param name="source">Who to ask.</param>
    /// <param name="folder">Where the panel is looking.</param>
    /// <returns>What it said, or nothing when it would not say.</returns>
    private static async Task<string> FreeAsync(IFileSource source, string folder)
    {
        try
        {
            return await source.FreeAsync(folder, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    private static async Task<(IReadOnlyList<FileEntry> Entries, string Error)> ReadAsync(
        IFileSource source,
        string folder,
        bool hidden)
    {
        try
        {
            return (await source.ListAsync(folder, hidden, CancellationToken.None).ConfigureAwait(false), "");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ([], error.Message);
        }
    }

    private void Landed(
        IFileSource source,
        string folder,
        (IReadOnlyList<FileEntry> Entries, string Error) read,
        string cursor)
    {
        if (!ReferenceEquals(source, _state.Source) || folder != _state.Folder)
        {
            return;
        }

        _loading = false;
        _error = read.Error;
        _entries.Clear();

        foreach (var entry in read.Entries)
        {
            if (Kept(entry))
            {
                _entries.Add(entry);
            }
        }

        Sort();
        Point(cursor);
    }

    private bool Kept(FileEntry entry) =>
        _state.Filter.Length == 0 || entry.IsFolder ||
        entry.Name.Contains(_state.Filter, StringComparison.OrdinalIgnoreCase);

    private void Sort()
    {
        _entries.Sort((first, second) => Listing.Compare(first, second, _state.Sorting, _state.Descending));
        _table.Items = _entries;
    }

    private void Up()
    {
        if (_state.Source.Parent(_state.Folder) is not { } parent)
        {
            return;
        }

        Enter(parent, leaving: true);
    }

    private void Mark()
    {
        if (Current is not { IsParent: false } current)
        {
            return;
        }

        if (!_state.Marks.TryAdd(current.Name))
        {
            _state.Marks.Remove(current.Name);
        }

        _table.Selected++;
    }

    private void Enter(string folder, bool leaving)
    {
        var left = _state.Source.NameOf(_state.Folder);

        _state.GoTo(folder);
        _state.Cursor = leaving ? left : "";

        Reload();

        if (!leaving)
        {
            _table.Selected = 0;
        }
    }

    private static string Describe(FileEntry entry)
    {
        var what = entry.IsFolder ? "folder" : Sizes.Brief(entry.Size);
        var said = $"{entry.Name} · {what}";

        return entry.IsReadOnly ? $"{said} · read-only" : said;
    }

    private string Shortened(string folder)
    {
        var room = Math.Max(TitleFloor, _width - TitleChrome);

        if (folder.Length <= room)
        {
            return folder;
        }

        var tail = folder[^(room - 1)..];
        var cut = tail.IndexOfAny(['/', '\\']);

        return "…" + (cut < 0 ? tail : tail[cut..]);
    }

    /// <summary>
    /// The rule down the left edge of the panel being worked in. The other panel has none: the step in
    /// the background already tells the two apart, and a second rule beside it would be a mark that
    /// means nothing competing with the one that means everything.
    /// </summary>
    private static TermColor Focused => Skin.Paint(Skin.Crimson, Skin.Crimson);

    private Rgb Beneath => IsFocused ? Skin.Lit : Skin.Unlit;

    /// <summary>
    /// Where the panel is looking, as a trail rather than a path: the separators recede, the folder you
    /// are in is the one in bone. A path too long for the room loses its head, since the end of it is
    /// the part that says where you are.
    /// </summary>
    /// <param name="row">The row to draw on.</param>
    /// <param name="coat">The surface underneath.</param>
    private void Where(SurfaceRegion row, Skin.Coat coat)
    {
        var right = Tally();
        var room = Math.Max(0, row.Width - TextWidth.Of(right) - ColumnGap);
        var trail = Trail(coat);
        var column = 0;

        while (trail.Count > 4 && Spans(trail) > room)
        {
            trail.RemoveRange(0, 2);
            trail[0] = ("…", coat.Ghost);
        }

        if (Spans(trail) > room)
        {
            row.Write(0, 0, Paths.Shortened(_state.Source, _state.Folder, room), coat.Strong);
        }
        else
        {
            foreach (var (text, style) in trail)
            {
                row.Write(0, column, text, style);
                column += TextWidth.Of(text);
            }
        }

        if (right.Length > 0)
        {
            row.WriteLine(0, right, coat.Trace, Align.Right);
        }
    }

    /// <summary>How wide a set of pieces comes out.</summary>
    /// <param name="trail">The pieces.</param>
    /// <returns>The cells they take.</returns>
    private static int Spans(List<(string Text, TermColor Style)> trail)
    {
        var wanted = 0;

        foreach (var (text, _) in trail)
        {
            wanted += TextWidth.Of(text);
        }

        return wanted;
    }

    /// <summary>
    /// The pieces the trail is written from. A server is named first and in its own colour, since which
    /// machine a path is on matters more than any folder in it.
    /// </summary>
    /// <param name="coat">The surface underneath.</param>
    /// <returns>The pieces, in the order they are written, in pairs of separator and name.</returns>
    private List<(string Text, TermColor Style)> Trail(Skin.Coat coat)
    {
        var pieces = new List<(string, TermColor)>();
        var folder = Paths.Homed(_state.Source, _state.Folder);
        var names = folder.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

        if (_state.Source.IsRemote)
        {
            pieces.Add((_state.Source.Label, Skin.Paint(Skin.Sea, Beneath, TextStyle.Bold)));
            pieces.Add((" ", coat.Text));
        }
        else
        {
            pieces.Add((folder.StartsWith('/') ? "/" : "", coat.Ghost));
            pieces.Add(("", coat.Text));
        }

        for (var index = 0; index < names.Length; index++)
        {
            var last = index == names.Length - 1;

            pieces.Add((names[index], last ? coat.Strong : coat.Meta));
            pieces.Add((last ? "" : " / ", coat.Ghost));
        }

        if (names.Length > 0)
        {
            return pieces;
        }

        pieces.Add((folder, coat.Strong));
        pieces.Add(("", coat.Ghost));

        return pieces;
    }

    private string Tally()
    {
        if (_loading)
        {
            return "reading…";
        }

        var items = _entries.Count > 0 && _entries[0].IsParent ? _entries.Count - 1 : _entries.Count;
        var counted = items == 1 ? "1 item" : $"{items} items";

        return _free.Length == 0 ? counted : $"{counted} · {_free}";
    }

    /// <summary>The column heads, in small capitals, with the sort arrow on the one that is sorted by.</summary>
    /// <param name="row">The row to draw on.</param>
    /// <param name="coat">The surface underneath.</param>
    private void Heads(SurfaceRegion row, Skin.Coat coat)
    {
        var (name, size, date) = Widths(row.Width);

        Head(row, Kinds.TagWidth, name, "NAME", Sorting.Name, coat, Align.Left);

        if (size > 0)
        {
            Head(row, Kinds.TagWidth + name + ColumnGap, size, "SIZE", Sorting.Size, coat, Align.Right);
        }

        if (date > 0)
        {
            Head(row, Kinds.TagWidth + name + ColumnGap + size + ColumnGap, date, "MODIFIED", Sorting.Modified,
                coat, Align.Right);
        }
    }

    private void Head(
        SurfaceRegion row,
        int column,
        int width,
        string text,
        Sorting sorting,
        Skin.Coat coat,
        Align align)
    {
        var sorted = _state.Sorting == sorting;
        var arrow = _state.Descending ? " ↓" : " ↑";
        var whole = sorted ? text + arrow : text;
        var at = align == Align.Right ? column + width - TextWidth.Of(whole) : column;

        if (at < column || width <= 0)
        {
            return;
        }

        row.Write(0, at, text, coat.Label);

        if (sorted)
        {
            row.Write(0, at + TextWidth.Of(text), arrow, coat.Accent);
        }
    }

    /// <summary>
    /// One file. Every span carries its own colour, and the row under the cursor lightens all of them
    /// at once: a filled row with a faint neutral still on it is the one thing this design cannot have.
    /// </summary>
    /// <param name="row">The row to draw on.</param>
    /// <param name="entry">What is on it.</param>
    /// <param name="chosen">Whether the cursor is on it.</param>
    private void PaintRow(SurfaceRegion row, FileEntry entry, bool chosen)
    {
        var coat = IsFocused ? Skin.Lively : Skin.Quiet;
        var cursor = chosen && IsFocused;
        var marked = _state.Marks.Contains(entry.Name);
        var (name, size, date) = Widths(row.Width);

        row.Fill(cursor ? Skin.CursorRow
            : chosen ? Skin.Paint(Skin.Bone, Skin.Chip)
            : marked ? coat.MarkedRow
            : coat.Text);

        var tone = Kinds.ToneOf(entry);

        row.Write(0, 0, Kinds.Tag(entry), Tag(tone, cursor, chosen, marked, coat));
        row.Write(0, Kinds.TagWidth, TextWidth.Truncate(entry.Name, name),
            Name(tone, cursor, chosen, marked, coat));

        if (size > 0)
        {
            var what = entry.IsFolder ? "<DIR>" : Sizes.Brief(entry.Size);

            row.Write(0, Kinds.TagWidth + name + ColumnGap + size - TextWidth.Of(what), what,
                Quiet(cursor, chosen, marked, coat));
        }

        if (date <= 0)
        {
            return;
        }

        var when = Sizes.When(entry.Modified);

        row.Write(0, Kinds.TagWidth + name + ColumnGap + size + ColumnGap + date - TextWidth.Of(when), when,
            cursor ? Skin.CursorDate : chosen || !marked ? coat.Trace : coat.MarkedMeta);
    }

    private static TermColor Tag(Tone tone, bool cursor, bool chosen, bool marked, Skin.Coat coat)
    {
        if (cursor)
        {
            return Skin.CursorTag;
        }

        var back = chosen ? Skin.Chip : marked ? Skin.Blend(Skin.Crimson, 0.13, Under(coat)) : Under(coat);

        return tone switch
        {
            Tone.Folder => Skin.Paint(Skin.Sea, back),
            Tone.Protected => Skin.Paint(Skin.AmberRule, back),
            Tone.Ignorable => Skin.Paint(new(0x3A, 0x35, 0x3F), back),
            Tone.Parent => Skin.Paint(new(0x4A, 0x45, 0x50), back),
            _ => Skin.Paint(new(0x6E, 0x68, 0x70), back),
        };
    }

    private static TermColor Name(Tone tone, bool cursor, bool chosen, bool marked, Skin.Coat coat)
    {
        if (cursor)
        {
            return Skin.CursorName;
        }

        var back = chosen ? Skin.Chip : marked ? Skin.Blend(Skin.Crimson, 0.13, Under(coat)) : Under(coat);

        if (marked)
        {
            return Skin.Paint(Skin.Coral, back);
        }

        return tone switch
        {
            Tone.Protected => Skin.Paint(Skin.Amber, back),
            Tone.Ignorable => Skin.Paint(new(0x57, 0x51, 0x5F), back),
            Tone.Parent => Skin.Paint(new(0x6E, 0x68, 0x70), back),
            _ => Skin.Paint(Skin.Bone, back),
        };
    }

    private static TermColor Quiet(bool cursor, bool chosen, bool marked, Skin.Coat coat) => cursor
        ? Skin.CursorMeta
        : chosen
            ? Skin.Paint(new(0x8A, 0x83, 0x90), Skin.Chip)
            : marked
                ? coat.MarkedMeta
                : coat.Meta;

    private static Rgb Under(Skin.Coat coat) => ReferenceEquals(coat, Skin.Lively) ? Skin.Lit : Skin.Unlit;

    /// <summary>
    /// How wide the three columns come out. The name takes what is left, and on a panel too narrow for
    /// all three the date goes first and the size after it — a name with nothing beside it still says
    /// which file it is.
    /// </summary>
    /// <param name="width">The room there is.</param>
    /// <returns>The width of each column; nought for one that does not fit.</returns>
    private static (int Name, int Size, int Date) Widths(int width)
    {
        var left = width - Kinds.TagWidth;

        if (left >= MinimumName + ColumnGap + SizeWidth + ColumnGap + StampWidth)
        {
            return (left - ColumnGap - SizeWidth - ColumnGap - StampWidth, SizeWidth, StampWidth);
        }

        return left >= MinimumName + ColumnGap + SizeWidth
            ? (left - ColumnGap - SizeWidth, SizeWidth, 0)
            : (Math.Max(0, left), 0, 0);
    }

    /// <summary>
    /// The foot: what the cursor is on, or what has been marked. Marking turns the whole band the
    /// colour of a mark, so the panel says it is holding something without anything having to be read.
    /// </summary>
    /// <param name="row">The row to draw on.</param>
    /// <param name="coat">The surface underneath.</param>
    private void Foot(SurfaceRegion row, Skin.Coat coat)
    {
        if (_searching)
        {
            row.Write(0, 0, TextWidth.Truncate($"jump to  {_typed}", row.Width), coat.Accent);

            return;
        }

        if (_state.Marks.Count > 0)
        {
            var bytes = 0L;

            foreach (var entry in Targets())
            {
                bytes += entry.Size;
            }

            var held = bytes > 0
                ? $"{_state.Marks.Count} marked · {Sizes.Brief(bytes)}"
                : $"{_state.Marks.Count} marked";

            row.Fill(coat.MarkedRow);
            row.Write(0, 0, TextWidth.Truncate(held, row.Width), coat.Marked);
            row.WriteLine(0, "+ / − mark by pattern · * invert", coat.MarkedMeta, Align.Right);

            return;
        }

        if (_loading)
        {
            row.Write(0, 0, "reading the folder…", coat.Faded);

            return;
        }

        row.Write(0, 0, TextWidth.Truncate(Current is { } current ? Describe(current) : "nothing here", row.Width),
            coat.Meta);

        if (IsFocused && Current is not null)
        {
            row.WriteLine(0, "type to jump", coat.Ghost, Align.Right);
        }
    }
}
