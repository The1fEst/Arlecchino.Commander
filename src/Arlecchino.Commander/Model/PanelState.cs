using System;
using System.Collections.Generic;
using Arlecchino.Atoms.Local;
using Arlecchino.Commander.Files.Sources;

namespace Arlecchino.Commander.Model;

public sealed class PanelState
{
    private readonly List<string> _visited = [];

    private int _place;

    public PanelState(IFileSource source, string folder)
    {
        Source = source;
        Folder = folder;
        _visited.Add(folder);
    }

    public IFileSource Source { get; private set; }

    public string Folder { get; private set; }

    public string Cursor { get; set; } = "";

    public string Filter { get; set; } = "";

    /// <summary>
    /// What is marked in this panel, by name. A set atom rather than a <c>HashSet</c>, so a mark makes the
    /// frame stale by itself, and one made from anywhere other than the drawing thread is caught rather than
    /// tolerated.
    /// </summary>
    public LocalAtomsSet<string> Marks { get; } = new(comparer: StringComparer.OrdinalIgnoreCase);

    public bool ShowHidden { get; set; }

    public Sorting Sorting { get; set; }

    public bool Descending { get; set; }

    /// <summary>Every folder this panel has been in on the source it is on, the oldest first.</summary>
    public IReadOnlyList<string> Visited => _visited;

    public void GoTo(string folder)
    {
        Land(folder);

        if (_place < _visited.Count - 1)
        {
            _visited.RemoveRange(_place + 1, _visited.Count - _place - 1);
        }

        if (string.Equals(_visited[_place], folder, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _visited.Add(folder);
        _place = _visited.Count - 1;
    }

    /// <summary>Steps back to the folder the panel was in before this one.</summary>
    /// <returns>Where it landed, or <c>null</c> when this is as far back as it goes.</returns>
    public string? Back() => _place > 0 ? Landed(_place - 1) : null;

    /// <summary>Steps forward again after <see cref="Back"/>.</summary>
    /// <returns>Where it landed, or <c>null</c> when it is already at the newest folder.</returns>
    public string? Forward() => _place < _visited.Count - 1 ? Landed(_place + 1) : null;

    public void Connect(IFileSource source, string folder)
    {
        var replaced = Source;

        Source = source;

        _visited.Clear();
        _visited.Add(folder);
        _place = 0;

        Land(folder);

        if (replaced.IsRemote)
        {
            replaced.Dispose();
        }
    }

    private string Landed(int place)
    {
        _place = place;

        Land(_visited[place]);

        return _visited[place];
    }

    private void Land(string folder)
    {
        Folder = folder;
        Filter = "";
        Marks.Clear();
    }
}
