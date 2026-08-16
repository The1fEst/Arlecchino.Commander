using System;
using System.Collections.Generic;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Files.Watching;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Widgets.Lists;

namespace Arlecchino.Commander.Widgets.Panels;

/// <summary>
/// What one panel is showing and where it came from: the folder asked for away from the drawing thread,
/// what came back filtered and sorted into the rows, and the watch that says when to ask again.
/// </summary>
public sealed class PanelReading : IDisposable
{
    private readonly PanelState _state;
    private readonly PanelPaint _paint;
    private readonly ListBox<FileEntry> _table;
    private readonly List<FileEntry> _entries = [];
    private readonly FolderWatch _watch;

    private bool _shown = true;

    /// <summary>Sets the reading up over one panel.</summary>
    /// <param name="state">What that panel is showing.</param>
    /// <param name="paint">Told that a folder is being waited for, what went wrong, and how much room is left.</param>
    /// <param name="table">The rows, filled with what was read and pointed at what the cursor was on.</param>
    /// <param name="settings">Asked how often a server should be read again, and whether to watch at all.</param>
    /// <param name="operations">Asked whether files are being carried, which a server is not read during.</param>
    public PanelReading(
        PanelState state,
        PanelPaint paint,
        ListBox<FileEntry> table,
        Settings settings,
        Operations operations)
    {
        _state = state;
        _paint = paint;
        _table = table;
        _watch = new(() => settings.Watch, () => operations.IsBusy, () => FrameThread.Post(Refresh));
    }

    /// <summary>What the panel is showing, in the order it is shown.</summary>
    public IReadOnlyList<FileEntry> Entries => _entries;

    /// <summary>
    /// Whether the panel is one of the two on-screen. A tab that has been stepped away from stops watching,
    /// and reads its folder again on the way back.
    /// </summary>
    public bool IsShown
    {
        get => _shown;

        set
        {
            if (_shown == value)
            {
                return;
            }

            _shown = value;

            if (value)
            {
                Refresh();

                return;
            }

            _watch.Stop();
        }
    }

    /// <summary>
    /// Reads the folder and lands on it. Every source is read the same way, the disk included, and the
    /// frame that asked for it carries on drawing meanwhile, saying that it is loading.
    /// </summary>
    public void Reload() => Read(loudly: true);

    /// <summary>
    /// Reads the folder again without saying so, which is what the watch asks for. What the panel is
    /// showing stays there until the answer comes back and replaces it.
    /// </summary>
    public void Refresh() => Read(loudly: false);

    /// <summary>Puts the rows in the order the panel is set to, and hands them to the list.</summary>
    public void Sort()
    {
        _entries.Sort((first, second) => Listing.Compare(first, second, _state.Sorting, _state.Descending));
        _table.Items = _entries;
    }

    /// <summary>Stops watching, leaving the reading able to start it again.</summary>
    public void Stop() => _watch.Stop();

    /// <summary>Stops watching for good, so that nothing is read after the panel has gone.</summary>
    public void Dispose() => _watch.Dispose();

    private void Read(bool loudly)
    {
        var cursor = _state.Cursor.Length > 0 ? _state.Cursor : _table.SelectedItem?.Name ?? "";
        var source = _state.Source;
        var folder = _state.Folder;
        var hidden = _state.ShowHidden;

        _state.Cursor = "";

        if (loudly)
        {
            _paint.Loading = true;
        }

        FrameThread.Post(async () =>
        {
            Landed(source, folder, hidden, await Listing.ReadAsync(source, folder, hidden), cursor);

            var free = await Listing.FreeAsync(source, folder);

            if (ReferenceEquals(source, _state.Source) && folder == _state.Folder)
            {
                _paint.Free = free;
            }
        });
    }

    /// <summary>
    /// What came back, taken only while the panel is still where it was when it was asked. A folder left
    /// during the wait has a reading of its own on the way, and this one is no longer about anything.
    /// </summary>
    /// <param name="source">Who was asked.</param>
    /// <param name="folder">Which folder.</param>
    /// <param name="hidden">Whether the hidden files were asked for.</param>
    /// <param name="read">What came back.</param>
    /// <param name="cursor">The name the cursor was on before.</param>
    private void Landed(
        IFileSource source,
        string folder,
        bool hidden,
        (IReadOnlyList<FileEntry> Entries, string Error) read,
        string cursor)
    {
        if (!ReferenceEquals(source, _state.Source) || folder != _state.Folder)
        {
            return;
        }

        _paint.Loading = false;
        _paint.Error = read.Error;
        _entries.Clear();

        foreach (var entry in read.Entries)
        {
            _entries.Add(entry);
        }

        Sort();
        Point(cursor);
        Watching(source, folder, hidden, read);
    }

    /// <summary>
    /// Puts the watch on what was just read. A folder that would not be read is not watched either, since
    /// there is nothing to compare a later reading against.
    /// </summary>
    /// <param name="source">Where the panel is looking.</param>
    /// <param name="folder">Which folder.</param>
    /// <param name="hidden">Whether the hidden files were asked for.</param>
    /// <param name="read">What came back.</param>
    private void Watching(
        IFileSource source,
        string folder,
        bool hidden,
        (IReadOnlyList<FileEntry> Entries, string Error) read)
    {
        if (!_shown)
        {
            return;
        }

        if (read.Error.Length > 0)
        {
            _watch.Stop();

            return;
        }

        _watch.Follow(source, folder, hidden, read.Entries);
    }

    /// <summary>Puts the cursor back on a name, and leaves it where it landed when that name has gone.</summary>
    /// <param name="name">What it was on before the reading.</param>
    private void Point(string name)
    {
        for (var index = 0; index < _entries.Count; index++)
        {
            if (string.Equals(_entries[index].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                _table.Selected = index;

                return;
            }
        }
    }
}
