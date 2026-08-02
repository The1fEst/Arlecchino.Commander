using System;
using System.Collections.Generic;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Widgets.Panel;
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
    protected FilePanel Here => _panels.Active;

    /// <summary>The other one.</summary>
    protected FilePanel There => _panels.Passive;

    /// <summary>
    /// Whether there is nothing to act on, said out loud when there is not. Every operation begins by
    /// asking this, so the answer to "why did nothing happen" is on the screen rather than guessed at.
    /// </summary>
    /// <param name="sources">What was going to be acted on.</param>
    /// <returns><c>true</c> when there is nothing.</returns>
    protected bool Nothing(IReadOnlyList<FileEntry> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        if (sources.Count > 0)
        {
            return false;
        }

        State.Output = Loc(LocString.SaidNothingSelected);

        return true;
    }
}
