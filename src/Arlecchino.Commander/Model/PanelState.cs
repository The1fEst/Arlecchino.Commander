using System;
using System.Collections.Generic;
using Arlecchino.Atoms.Local;
using Arlecchino.Commander.Files.Sources;

namespace Arlecchino.Commander.Model;

public sealed class PanelState
{
    private readonly List<string> _history = [];

    private int _place;

    public PanelState(IFileSource source, string folder)
    {
        Source = source;
        Folder = folder;
        _history.Add(folder);
    }

    public IFileSource Source { get; private set; }

    public string Folder { get; private set; }

    public string Cursor { get; set; } = "";

    /// <summary>
    /// What is marked in this panel, by name. A set atom rather than a <c>HashSet</c>, so a mark makes the
    /// frame stale by itself, and one made from anywhere other than the drawing thread is caught rather than
    /// tolerated.
    /// </summary>
    public LocalAtomsSet<string> Marks { get; } = new(comparer: StringComparer.OrdinalIgnoreCase);

    public bool ShowHidden { get; set; }

    public Sorting Sorting { get; set; }

    public bool Descending { get; set; }

    public void GoTo(string folder)
    {
        Land(folder);

        if (_place < _history.Count - 1)
        {
            _history.RemoveRange(_place + 1, _history.Count - _place - 1);
        }

        if (string.Equals(_history[_place], folder, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _history.Add(folder);
        _place = _history.Count - 1;
    }

    /// <summary>Steps back to the folder the panel was in before this one.</summary>
    /// <returns>Where it landed, or <c>null</c> when this is as far back as it goes.</returns>
    public string? Back() => _place > 0 ? Landed(_place - 1) : null;

    /// <summary>Steps forward again after <see cref="Back"/>.</summary>
    /// <returns>Where it landed, or <c>null</c> when it is already at the newest folder.</returns>
    public string? Forward() => _place < _history.Count - 1 ? Landed(_place + 1) : null;

    public void Connect(IFileSource source, string folder)
    {
        var tail = Source;

        Source = source;

        _history.Clear();
        _history.Add(folder);
        _place = 0;

        Land(folder);

        if (tail.IsRemote)
        {
            tail.Dispose();
        }
    }

    private string Landed(int place)
    {
        _place = place;

        Land(_history[place]);

        return _history[place];
    }

    private void Land(string folder)
    {
        Folder = folder;
        Marks.Clear();
    }
}
