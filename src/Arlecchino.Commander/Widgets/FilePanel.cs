using System;
using System.Collections.Generic;
using System.IO;
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
    private const int StampWidth = 14;
    private const int TitleChrome = 4;
    private const int TitleFloor = 12;
    private const int TitleGuess = 44;

    private readonly PanelState _state;
    private readonly ArlecchinoKeymap _keymap;
    private readonly Table<FileEntry> _table;
    private readonly List<FileEntry> _entries = [];

    private string _error = "";
    private string _typed = "";
    private int _width = TitleGuess;
    private bool _loading;
    private bool _searching;

    public FilePanel(PanelState state, ArlecchinoKeymap keymap)
    {
        _state = state;
        _keymap = keymap;

        _table = new(keymap)
        {
            Columns =
            [
                new() { Header = () => Marked("Name", Sorting.Name), Cell = static entry => entry.Name },
                new()
                {
                    Header = () => Marked("Size", Sorting.Size),
                    Cell = static entry => entry.IsFolder ? "<DIR>" : Sizes.Brief(entry.Size),
                    Width = SizeWidth,
                    AlignRight = true,
                },
                new()
                {
                    Header = () => Marked("Modified", Sorting.Modified),
                    Cell = static entry => Sizes.Stamp(entry.Modified),
                    Width = StampWidth,
                },
            ],
            ItemStyle = Paint,
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

    public FileEntry? Current => _table.SelectedRow;

    /// <summary>Whether the search that runs while you type is on, in which case typing is its own.</summary>
    public bool IsSearching => _searching;

    public string Title
    {
        get
        {
            var where = _state.Source.IsRemote
                ? $"{_state.Source.Label}:{_state.Folder}"
                : _state.Folder;

            return _state.Filter.Length == 0
                ? Shortened(where)
                : $"{Shortened(where)}  [{_state.Filter}]";
        }
    }

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

    public void GoTo(string folder)
    {
        if (!_state.Source.FolderExists(folder))
        {
            return;
        }

        _state.GoTo(folder);
        Reload();
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

            if (!_state.Marks.Add(entry.Name))
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

    public void Reload()
    {
        var cursor = _state.Cursor.Length > 0 ? _state.Cursor : Current?.Name ?? "";
        var source = _state.Source;
        var folder = _state.Folder;
        var hidden = _state.ShowHidden;

        _state.Cursor = "";

        if (!source.IsRemote)
        {
            Landed(source, folder, Read(source, folder, hidden), cursor);
            return;
        }

        _loading = true;

        Task.Run(() =>
        {
            var read = Read(source, folder, hidden);

            FrameThread.Post(() => Landed(source, folder, read, cursor));
        });
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
            char.IsControl(key.KeyChar) || key.KeyChar == '\0')
        {
            _searching = false;

            return key.Key is ConsoleKey.Escape or ConsoleKey.Enter;
        }

        _typed += key.KeyChar;
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

    public SurfaceRegion Draw(SurfaceRegion region)
    {
        if (region.IsEmpty)
        {
            return region;
        }

        _width = region.Width;

        if (_error.Length > 0)
        {
            region.WriteLine(0, _error, Theme.Error);
            return region.Rows(region.Height, 0);
        }

        var (rows, footer) = region.SplitTop(region.Height - 1);

        _table.Draw(rows);

        if (_searching)
        {
            footer.WriteLine(0, $"search: {_typed}", Theme.Accent);
        }
        else
        {
            footer.WriteLine(0, _loading ? "loading…" : Summary(), Theme.Muted);
        }

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

        if (key.Modifiers == 0 && Grouping(key.KeyChar))
        {
            return FocusResult.Handled;
        }

        if (!_keymap.Erase.Matches(key))
        {
            return _table.Handle(key);
        }

        Up();

        return FocusResult.Handled;
    }

    public FocusResult HandleMouse(MouseEvent mouse) => _table.HandleMouse(mouse);

    private static (IReadOnlyList<FileEntry> Entries, string Error) Read(
        IFileSource source,
        string folder,
        bool hidden)
    {
        try
        {
            return (source.List(folder, hidden), "");
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
        _table.Rows = _entries;
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

        if (!_state.Marks.Add(current.Name))
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

    private string Summary()
    {
        if (_state.Marks.Count == 0)
        {
            return Current is { } current ? Describe(current) : "empty";
        }

        var bytes = 0L;

        foreach (var entry in Targets())
        {
            bytes += entry.Size;
        }

        return $"{_state.Marks.Count} marked  {Sizes.Grouped(bytes)} bytes";
    }

    private static string Describe(FileEntry entry)
    {
        var what = entry.IsFolder ? "<DIR>" : $"{Sizes.Grouped(entry.Size)} bytes";

        return entry.IsReadOnly ? $"{entry.Name}  {what}  read-only" : $"{entry.Name}  {what}";
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

    private string Marked(string header, Sorting sorting) => _state.Sorting == sorting
        ? $"{header} {(_state.Descending ? "↓" : "↑")}"
        : header;

    private TermColor Paint(FileEntry entry)
    {
        if (_state.Marks.Contains(entry.Name))
        {
            return Theme.Warning;
        }

        return entry.IsFolder
            ? Theme.Accent
            : entry.IsHidden
                ? Theme.Muted
                : Theme.Default;
    }
}
