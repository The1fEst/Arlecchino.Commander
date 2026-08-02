using System;
using System.Collections.Generic;
using System.Threading;
using Arlecchino.Commander.Files.Work;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Panel;
using Arlecchino.Commander.Widgets.Dialogs;
using Arlecchino.State;

namespace Arlecchino.Commander.Views.Doing;

/// <summary>
/// The five things the function keys do to files: copy, move, rename, make a folder, delete.
///
/// None of them happens on the key press. Each opens the one dialog, which says in words what is
/// about to happen and to how much, and only then hands the work to something that runs on its own —
/// the panels stay usable while a copy of eight gigabytes is going on behind them.
/// </summary>
public sealed class Deeds : PanelWork
{
    private readonly Operations _operations;

    /// <summary>Sets the operations up over a pair of panels.</summary>
    /// <param name="dialogs">How anything is asked.</param>
    /// <param name="operations">What carries the work out.</param>
    /// <param name="state">Where the last word said is kept.</param>
    /// <param name="panels">The two panels on screen.</param>
    public Deeds(Dialogs dialogs, Operations operations, ArlecchinoState state, Pair panels)
        : base(dialogs, state, panels) => _operations = operations;

    /// <summary>Copies what is marked to where the other panel is looking.</summary>
    public void Copy()
    {
        var from = Here;
        var to = There;
        var sources = from.Targets();

        if (Nothing(sources))
        {
            return;
        }

        Dialogs.Ask(new()
        {
            Title = sources.Count == 1 ? Loc(LocString.Copy) : Loc(LocString.CopyManyTitle, sources.Count),
            Key = "F5",
            Verb = Loc(LocString.Copy),
            Weight = Weight.Moves,
            Items = sources,
            FieldLabel = Loc(LocString.OperationWhere),
            Host = to.Source.IsRemote ? to.Source.Label : "",
            Value = to.Folder,
            FieldHint = Carrying(sources, to),
            Over = to.Source,
            Confirm = asking => _operations.Copy(from.Source, sources, to.Source, asking.Value),
        });
    }

    /// <summary>
    /// Moves what is marked. Whether it is a move at all depends on where it lands: within one volume
    /// nothing is copied and only the names change, and across two it is a copy followed by a delete —
    /// which is a different promise, and the dialog says which one is being made.
    /// </summary>
    public void Move()
    {
        var from = Here;
        var to = There;
        var sources = from.Targets();

        if (Nothing(sources))
        {
            return;
        }

        var across = !ReferenceEquals(from.Source, to.Source) ||
            !from.Source.SameVolume(from.Folder, to.Folder);

        Dialogs.Ask(new()
        {
            Title = sources.Count == 1 ? Loc(LocString.Move) : Loc(LocString.MoveManyTitle, sources.Count),
            Key = "F6",
            Verb = Loc(LocString.Move),
            Weight = Weight.Moves,
            Items = sources,
            FieldLabel = Loc(LocString.OperationWhereTo),
            Host = to.Source.IsRemote ? to.Source.Label : "",
            Value = to.Folder,
            FieldHint = across ? Loc(LocString.MoveAcross) : Loc(LocString.MoveWithin),
            Over = to.Source,
            Confirm = asking => _operations.Move(from.Source, sources, to.Source, asking.Value),
        });
    }

    /// <summary>Renames what is under the cursor, in the folder it is already in.</summary>
    public void Rename()
    {
        var panel = Here;

        if (panel.Current is not { IsParent: false } current)
        {
            return;
        }

        Dialogs.Ask(new()
        {
            Title = Loc(LocString.Rename),
            Subtitle = current.Name,
            Key = "Shift+F6",
            Verb = Loc(LocString.Rename),
            Weight = Weight.Reversible,
            FieldLabel = Loc(LocString.OperationNewName),
            Value = current.Name,
            FieldHint = Loc(LocString.RenameSameFolder),
            Note = asking => Taken(panel, current.Name, asking.Value)
                ? new(Loc(LocString.RenameTaken), true)
                : null,
            Confirm = asking =>
            {
                panel.State.Cursor = panel.Source.NameOf(asking.Value);
                _operations.Rename(panel.Source, current, panel.Source.Combine(panel.Folder, asking.Value));
            },
        });
    }

    /// <summary>Makes a folder where the panel is looking.</summary>
    public void MakeFolder()
    {
        var panel = Here;

        Dialogs.Ask(new()
        {
            Title = Loc(LocString.NewFolder),
            Subtitle = Loc(LocString.NewFolderInside, Paths.Homed(panel.Source, panel.Folder)),
            Key = "F7",
            Verb = Loc(LocString.NewFolderVerb),
            Weight = Weight.Reversible,
            FieldLabel = Loc(LocString.OperationName),
            FieldHint = Loc(LocString.NewFolderSlashes),
            Note = static asking => asking.Value.Contains('/', StringComparison.Ordinal)
                ? new(Loc(LocString.NewFolderNested))
                : null,
            Options = [new(Loc(LocString.NewFolderJumpTo), true)],
            Confirm = asking => Making(panel, asking),
        });
    }

    /// <summary>Deletes what is marked, having said how much of it there is and that it is final.</summary>
    public void Delete()
    {
        var panel = Here;
        var sources = panel.Targets();

        if (Nothing(sources))
        {
            return;
        }

        var bytes = 0L;
        var folders = 0;

        foreach (var entry in sources)
        {
            bytes += entry.Size;
            folders += entry.IsFolder ? 1 : 0;
        }

        Dialogs.Ask(new()
        {
            Title = sources.Count == 1 ? Loc(LocString.Delete) : Loc(LocString.DeleteManyTitle, sources.Count),
            Subtitle = Loc(LocString.DeleteFrom, Paths.Homed(panel.Source, panel.Folder)),
            Key = "F8",
            Verb = Loc(LocString.Delete),
            Weight = Weight.Destroys,
            Items = sources,
            ItemsLabel = Loc(LocString.OperationGoingAway),
            Note = _ => new(Losing(bytes, folders), true),
            Confirm = _ => _operations.Delete(panel.Source, sources),
        });
    }

    /// <summary>What deleting would cost, in the words that fit what is being deleted.</summary>
    /// <param name="bytes">How much is named in the list.</param>
    /// <param name="folders">How many folders are in it.</param>
    /// <returns>The words.</returns>
    private static string Losing(long bytes, int folders)
    {
        if (folders == 0)
        {
            return Loc(LocString.DeletePlain, Sizes.Brief(bytes));
        }

        var these = folders == 1 ? Loc(LocString.DeleteOneFolder) : Loc(LocString.DeleteManyFolders);

        return bytes == 0
            ? Loc(LocString.DeleteFolders, these)
            : Loc(LocString.DeleteNamed, Sizes.Brief(bytes), these);
    }

    /// <summary>Makes the folder that was asked for, and lands the cursor on it when that was asked too.</summary>
    /// <param name="panel">Where it goes.</param>
    /// <param name="asking">What was answered.</param>
    private void Making(FilePanel panel, Operation asking)
    {
        var name = asking.Value;

        if (name.Trim().Length == 0)
        {
            State.Output = Loc(LocString.SaidNameNeeded);

            return;
        }

        Answers.From(
            () => FileTasks.CreateFolderAsync(panel.Source, panel.Folder, name, CancellationToken.None),
            created =>
            {
                if (created is null)
                {
                    State.Output = Loc(LocString.SaidCouldNotCreate, name);

                    return;
                }

                if (asking.Ticked(Loc(LocString.NewFolderJumpTo)))
                {
                    panel.State.Cursor = panel.Source.NameOf(created);
                }

                panel.Reload();
            });
    }

    /// <summary>
    /// How much is being carried, short enough to sit beside the field. A folder is not measured — that
    /// is a walk of its own — so it is counted rather than weighed.
    /// </summary>
    /// <param name="sources">What is being carried.</param>
    /// <param name="to">Where to.</param>
    /// <returns>The words.</returns>
    private static string Carrying(IReadOnlyList<FileEntry> sources, FilePanel to)
    {
        var bytes = 0L;
        var folders = 0;

        foreach (var entry in sources)
        {
            bytes += entry.Size;
            folders += entry.IsFolder ? 1 : 0;
        }

        var size = bytes > 0 ? Sizes.Brief(bytes) : "";
        var trees = folders == 0
            ? ""
            : folders == 1 ? Loc(LocString.CarryingFolder) : Loc(LocString.CarryingFolders, folders);
        var both = size.Length > 0 && trees.Length > 0 ? Loc(LocString.CarryingBoth, size, trees) : size + trees;

        return to.Source.IsRemote ? Loc(LocString.CarryingOverSftp, both) : both;
    }

    /// <summary>
    /// Whether something else in the folder is already called this. The name being renamed does not
    /// count against itself, or a dialog opened on a file would warn about the file it is renaming.
    /// </summary>
    /// <param name="panel">The folder.</param>
    /// <param name="was">What it is called now.</param>
    /// <param name="wanted">What it is being called.</param>
    /// <returns><c>true</c> when the name is taken by something else.</returns>
    private static bool Taken(FilePanel panel, string was, string wanted)
    {
        if (wanted == was || wanted.Trim().Length == 0)
        {
            return false;
        }

        foreach (var entry in panel.Entries)
        {
            if (!entry.IsParent && string.Equals(entry.Name, wanted, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
