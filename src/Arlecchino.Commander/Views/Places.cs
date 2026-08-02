using System;
using System.Collections.Generic;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets;
using Arlecchino.State;

namespace Arlecchino.Commander.Views;

/// <summary>
/// The folders worth going back to: the ones a panel has been in, kept by the panel, and the ones
/// worth keeping, kept by hand. Both are the same question asked of two different lists, so both are
/// asked through the same list dialog.
/// </summary>
public sealed class Places
{
    private readonly Dialogs _dialogs;
    private readonly Panels _panels;
    private readonly ArlecchinoState _state;

    /// <summary>Sets the two lists up.</summary>
    /// <param name="dialogs">How anything is asked.</param>
    /// <param name="panels">Where the kept folders live.</param>
    /// <param name="state">Where the last word said is kept.</param>
    public Places(Dialogs dialogs, Panels panels, ArlecchinoState state)
    {
        _dialogs = dialogs;
        _panels = panels;
        _state = state;
    }

    /// <summary>Opens where a panel has been, newest first and each folder named once.</summary>
    /// <param name="panel">Whose history.</param>
    public void History(FilePanel panel)
    {
        ArgumentNullException.ThrowIfNull(panel);

        var been = panel.State.Visited;
        var listed = new List<string>(been.Count);

        for (var index = been.Count - 1; index >= 0; index--)
        {
            if (!Has(listed, been[index]))
            {
                listed.Add(been[index]);
            }
        }

        if (listed.Count < 2)
        {
            _state.Output = Loc(LocString.SaidNotBeenElsewhere);

            return;
        }

        _dialogs.Pick(Loc(LocString.FoldersBeenIn), listed, panel.GoTo);
    }

    /// <summary>
    /// The kept folders, with the two entries that keep the list itself: adding where the panel is
    /// now, and dropping one that has served its purpose.
    /// </summary>
    /// <param name="panel">The panel the list acts on.</param>
    public void Hotlist(FilePanel panel)
    {
        ArgumentNullException.ThrowIfNull(panel);

        var add = Loc(LocString.HotlistAdd);
        var drop = Loc(LocString.HotlistDrop);
        var listed = new List<string>(_panels.Hotlist) { add };

        if (_panels.Hotlist.Count > 0)
        {
            listed.Add(drop);
        }

        _dialogs.Pick(Loc(LocString.Hotlist), listed, chosen =>
        {
            if (chosen == add)
            {
                Remember(panel.Folder);
            }
            else if (chosen == drop)
            {
                _dialogs.Pick(Loc(LocString.PickForget), new List<string>(_panels.Hotlist), Forget);
            }
            else
            {
                panel.GoTo(chosen);
            }
        });
    }

    /// <summary>Keeps a folder, unless it is kept already.</summary>
    /// <param name="folder">The folder.</param>
    public void Remember(string folder)
    {
        if (Has(_panels.Hotlist, folder))
        {
            _state.Output = Loc(LocString.HotlistAlready);

            return;
        }

        _panels.Hotlist.Add(folder);
        _state.Output = Loc(LocString.HotlistOn, folder);
    }

    /// <summary>Whether a folder is already in a list, whatever it was typed as.</summary>
    /// <param name="folders">The list.</param>
    /// <param name="folder">The folder.</param>
    /// <returns><c>true</c> when it is there.</returns>
    private static bool Has(IReadOnlyList<string> folders, string folder)
    {
        foreach (var kept in folders)
        {
            if (string.Equals(kept, folder, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Stops keeping a folder.</summary>
    /// <param name="folder">The folder.</param>
    private void Forget(string folder)
    {
        _panels.Hotlist.Remove(folder);
        _state.Output = Loc(LocString.HotlistOff, folder);
    }
}
