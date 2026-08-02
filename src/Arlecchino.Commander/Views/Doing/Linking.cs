using System.Threading;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Commander.Widgets.Dialogs;
using Arlecchino.State;

namespace Arlecchino.Commander.Views.Doing;

/// <summary>
/// Making one name stand for another. A link is put in the other panel when both are looking at the
/// same machine and beside itself when they are not — a link across two machines would point at
/// nothing, and making one anyway is a way of finding that out an hour later.
/// </summary>
public sealed class Linking : PanelWork
{
    /// <summary>Sets linking up over a pair of panels.</summary>
    /// <param name="dialogs">How anything is asked.</param>
    /// <param name="state">Where the last word said is kept.</param>
    /// <param name="panels">The two panels on screen.</param>
    public Linking(Dialogs dialogs, ArlecchinoState state, Pair panels)
        : base(dialogs, state, panels)
    {
    }

    /// <summary>Links what is under the cursor.</summary>
    /// <param name="hard">Whether to make a hard link rather than a symbolic one.</param>
    public void Make(bool hard)
    {
        var panel = Here;
        var other = There;

        if (panel.Current is not { IsParent: false } current)
        {
            State.Output = Loc(LocString.SaidNothingToLink);

            return;
        }

        var beside = Alike(panel.Source, other.Source) ? other : panel;
        var kind = hard ? Loc(LocString.LinkHard) : Loc(LocString.LinkSymbolic);

        Dialogs.AskFor(
            kind,
            Loc(LocString.OperationNamed, Paths.Homed(beside.Source, beside.Folder)),
            current.Name,
            Loc(LocString.LinkVerb),
            name => Answers.From(
                () => beside.Source.TryLinkAsync(
                    beside.Source.Combine(beside.Folder, name.Trim()),
                    current.Path,
                    hard,
                    CancellationToken.None),
                made =>
                {
                    if (made)
                    {
                        State.Output = Loc(LocString.SaidLinkMade, kind, name.Trim());
                        beside.Reload();

                        return;
                    }

                    State.Output = Loc(
                        LocString.SaidLinkRefused,
                        beside.Source.Label,
                        kind.ToLowerInvariant());
                }));
    }

    /// <summary>
    /// Whether two panels are looking at the same machine. Each panel holds a source of its own even
    /// when both are local, so this asks what the source reaches rather than which object it is.
    /// </summary>
    /// <param name="one">One panel's source.</param>
    /// <param name="other">The other's.</param>
    /// <returns><c>true</c> when a path from one means the same thing to the other.</returns>
    private static bool Alike(IFileSource one, IFileSource other) =>
        one.IsRemote == other.IsRemote && one.Label == other.Label;
}
