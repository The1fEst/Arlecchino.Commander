using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Editing;
using Arlecchino.Focus;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Navigation;
using Arlecchino.Rendering;
using Arlecchino.Widgets;
using Arlecchino.Widgets.Lists;

namespace Arlecchino.Commander.Widgets.Panels;

public sealed class FilePanel : IArlecchinoInteractiveWidget, IDisposable
{
    private readonly PanelState _state;
    private readonly ArlecchinoKeymap _keymap;
    private readonly KeyText _keys;
    private readonly ListBox<FileEntry> _table;
    private readonly PanelPaint _paint = new();
    private readonly SearchLine _searchLine;
    private readonly PanelReading _reading;

    /// <summary>Creates a panel over one side's state.</summary>
    /// <param name="state">What that side is showing.</param>
    /// <param name="keymap">Keys to obey.</param>
    /// <param name="keys">
    /// Turns a key press into the character it types, so the search that runs while you type and the
    /// marking keys work with a Cyrillic layout switched on.
    /// </param>
    /// <param name="terminal">Reached for the clipboard when what is being searched for is copied.</param>
    /// <param name="settings">Asked how often a server should be read again, and whether to watch at all.</param>
    /// <param name="operations">Asked whether files are being carried, which a server is not read during.</param>
    public FilePanel(
        PanelState state,
        ArlecchinoKeymap keymap,
        KeyText keys,
        IArlecchinoTerminal terminal,
        Settings settings,
        Operations operations)
    {
        _state = state;
        _keymap = keymap;
        _keys = keys;
        _searchLine = new(keymap, keys, terminal, Nearest);

        _table = new(keymap)
        {
            Render = static entry => entry.Name,
            PaintRow = (row, entry, chosen) => PanelRow.Draw(row, entry, _state, chosen, IsFocused),
            OnActivate = Activate,
        };

        _reading = new(state, _paint, _table, settings, operations);

        Reload();
    }

    /// <summary>
    /// Whether this panel is one of the two on-screen. A tab that has been stepped away from stops watching,
    /// and reads its folder again on the way back.
    /// </summary>
    public bool IsShown
    {
        get => _reading.IsShown;
        set => _reading.IsShown = value;
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
    public bool IsSearching => _searchLine.IsRunning;

    /// <summary>
    /// What is being searched for, with its caret and selection, which the foot of the panel shows.
    /// </summary>
    public ITextEntry Typed => _searchLine.Entry;

    /// <summary>What the panel is showing, in the order it is shown.</summary>
    public IReadOnlyList<FileEntry> Entries => _reading.Entries;

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

        foreach (var entry in _reading.Entries)
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

    public void Middle() => _table.Selected = _reading.Entries.Count / 2;

    public void Bottom() => _table.Selected = Math.Max(0, _reading.Entries.Count - 1);

    /// <summary>
    /// Starts the search that runs while you type, which moves the cursor to the first name that
    /// begins with what has been typed so far. Escape, Enter or any other key ends it.
    /// </summary>
    public void Search() => _searchLine.Start();

    /// <summary>Adds pasted text to that search, when there is one running to add it to.</summary>
    /// <param name="text">What was pasted.</param>
    /// <returns><c>true</c> when the search took it.</returns>
    public bool Paste(string text) => _searchLine.Paste(text);

    /// <summary>Marks, or unmarks, every file whose name fits a shell pattern.</summary>
    /// <param name="pattern">The pattern, as <c>*.cs</c> or <c>a*,b*</c>.</param>
    /// <param name="marking"><c>true</c> to mark what fits, <c>false</c> to unmark it.</param>
    public void MarkGroup(string pattern, bool marking) =>
        Marking.Group(_state, _reading.Entries, pattern, marking);

    /// <summary>Marks what is not marked and unmarks what is.</summary>
    public void Invert() => Marking.Invert(_state, _reading.Entries);

    /// <summary>
    /// Puts the panel on another source, stopping the watch first: the source being left is closed with it,
    /// and a reading on its way to a closed connection asks nothing.
    /// </summary>
    /// <param name="source">What to look at now.</param>
    /// <param name="folder">Where to land on it.</param>
    public void Connect(IFileSource source, string folder)
    {
        _reading.Stop();
        _state.Connect(source, folder);
        Reload();
    }

    /// <summary>Reads the folder and lands on it, saying meanwhile that it is being waited for.</summary>
    public void Reload() => _reading.Reload();

    /// <summary>Reads the folder again without saying so, which is what the watch asks for.</summary>
    public void Refresh() => _reading.Refresh();

    /// <summary>Stops watching the folder, for good.</summary>
    public void Dispose() => _reading.Dispose();

    public void SortBy(Sorting sorting)
    {
        _state.Descending = _state.Sorting == sorting && !_state.Descending;
        _state.Sorting = sorting;

        _reading.Sort();
    }

    /// <summary>
    /// Turns the order around without changing what it is ordered by. Asking for the same column again
    /// does this too, but only from the column it is already on — this says it about whichever one that is.
    /// </summary>
    public void Reverse()
    {
        _state.Descending = !_state.Descending;

        _reading.Sort();
    }

    /// <summary>Moves the cursor to the first name the typed letters begin, keeping it where it is otherwise.</summary>
    /// <param name="typed">The letters spelled so far.</param>
    private void Nearest(string typed)
    {
        var entries = _reading.Entries;

        for (var index = 0; index < entries.Count; index++)
        {
            if (!entries[index].Name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
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
    /// Draws the panel. What it looks like is <see cref="PanelPaint"/>; what is in it is this, and the
    /// two meet here — the paint hands back the rows the files go in, and the list fills them.
    /// </summary>
    /// <param name="region">The whole panel, rule included.</param>
    /// <returns>An empty region: the panel uses every row it is handed.</returns>
    public SurfaceRegion Draw(SurfaceRegion region)
    {
        var rows = _paint.Draw(region, this);

        if (!rows.IsEmpty)
        {
            _table.Draw(rows);
        }

        return region.Rows(region.Height, 0);
    }

    public FocusResult Handle(KeyPress key)
    {
        if (_searchLine.Handle(key))
        {
            return FocusResult.Handled;
        }

        if (_keymap.Mark.Matches(key) || key.Key == ConsoleKey.Insert)
        {
            Mark();
            return FocusResult.Handled;
        }

        if (key.Modifiers == 0 &&
            _keys.Resolve(key) is { } marking &&
            Marking.Typed(marking, _state, _reading.Entries, OnGroup))
        {
            return FocusResult.Handled;
        }

        return Steered(key) ?? _table.Handle(key);
    }

    /// <summary>
    /// The two letters an editor moves a cursor with, handed on as arrows so that paging and wrapping stay
    /// the list's business. Their neighbors <c>h</c> and <c>l</c> are commands the screen declares instead.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns>What became of it, or nothing when the key was neither.</returns>
    private FocusResult? Steered(KeyPress key)
    {
        if (key.Modifiers != 0 || _keys.Resolve(key) is not { } typed)
        {
            return null;
        }

        return char.ToLowerInvariant(typed) switch
        {
            'j' => _table.Handle(new(ConsoleKey.DownArrow)),
            'k' => _table.Handle(new(ConsoleKey.UpArrow)),
            _ => null,
        };
    }

    /// <summary>
    /// Clicks and the wheel. A click on the column heads sorts by the one that was hit, and anything lower
    /// belongs to the list.
    /// </summary>
    /// <param name="mouse">The event that arrived.</param>
    /// <returns>What became of it.</returns>
    public FocusResult HandleMouse(MouseEvent mouse)
    {
        if (_paint.Hit(mouse) is not { } sorting)
        {
            return _table.HandleMouse(mouse);
        }

        SortBy(sorting);

        return FocusResult.Handled;
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
        if (Marking.One(_state, Current))
        {
            _table.Selected++;
        }
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
}
