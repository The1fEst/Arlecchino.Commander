using System.Collections.Generic;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Commander.Widgets.Dialogs;
using Arlecchino.State;

namespace Arlecchino.Commander.Views.Doing;

/// <summary>
/// The ground every operation stands on: which panel is being worked in, which is being worked at,
/// how to ask before doing anything, and what to say when there is nothing to do it to.
/// </summary>
public abstract class PanelWork
{
    private readonly Pair _panels;

    /// <summary>Sets up whatever an operation needs to know about the screen it was asked from.</summary>
    /// <param name="dialogs">How anything is asked.</param>
    /// <param name="state">Where the last word said is kept.</param>
    /// <param name="panels">The two panels on screen.</param>
    protected PanelWork(Dialogs dialogs, ArlecchinoState state, Pair panels)
    {
        Dialogs = dialogs;
        State = state;
        _panels = panels;
    }

    /// <summary>How anything is asked.</summary>
    protected Dialogs Dialogs { get; }

    /// <summary>Where the last word said is kept.</summary>
    protected ArlecchinoState State { get; }

    /// <summary>The panel being worked in.</summary>
    protected FilePanel Active => _panels.Active;

    /// <summary>The other one.</summary>
    protected FilePanel Passive => _panels.Passive;

    /// <summary>
    /// Whether there is nothing to act on, said out loud when there is not. Every operation begins by
    /// asking this, so the answer to "why did nothing happen" is on the screen rather than guessed at.
    /// </summary>
    /// <param name="sources">What was going to be acted on.</param>
    /// <returns><c>true</c> when there is nothing.</returns>
    protected bool Nothing(IReadOnlyList<FileEntry> sources)
    {
        if (sources.Count > 0)
        {
            return false;
        }

        State.Output = Loc(LocString.SaidNothingSelected);

        return true;
    }

    /// <summary>
    /// Whether a source refuses to be written into, said out loud when it does. It is asked before the
    /// dialog rather than after, since an archive nothing can be written into will not change its mind.
    /// </summary>
    /// <param name="source">Where the work would be done.</param>
    /// <returns><c>true</c> when nothing can be written there.</returns>
    protected bool Sealed(IFileSource source)
    {
        if (source is not IPacksFiles { Packs: false } packer)
        {
            return false;
        }

        State.Output = packer.Refusal;

        return true;
    }
}
