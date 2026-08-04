using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Work;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Commander.Widgets.Dialogs;
using Arlecchino.State;

namespace Arlecchino.Commander.Views.Doing;

/// <summary>
/// Who may do what to a file, and whose it is. Both questions are asked of what is marked rather than
/// of one file at a time, because raising a bit on forty files one dialog at a time is not a thing
/// anybody does twice.
/// </summary>
public sealed class Rights : PanelWork
{
    private readonly Runner _runner;

    /// <summary>Sets the two up over a panel.</summary>
    /// <param name="dialogs">How anything is asked.</param>
    /// <param name="runner">The shell, for the one of these no protocol carries a request for.</param>
    /// <param name="state">Where the last word said is kept.</param>
    /// <param name="panels">The two panels on screen.</param>
    public Rights(Dialogs dialogs, Runner runner, ArlecchinoState state, Pair panels)
        : base(dialogs, state, panels) => _runner = runner;

    /// <summary>
    /// Sets the permissions of what is marked. The box opens on the permissions the first of them
    /// already has, so raising one bit does not mean typing the other eight from memory.
    /// </summary>
    public void Mode()
    {
        var panel = Here;
        var targets = panel.Targets();

        if (Nothing(targets))
        {
            return;
        }

        var folders = 0;

        foreach (var entry in targets)
        {
            folders += entry.IsFolder ? 1 : 0;
        }

        Answers.From(
            () => panel.Source.ModeAsync(targets[0], CancellationToken.None),
            current => Dialogs.Ask(new()
            {
                Title = targets.Count == 1
                    ? Loc(LocString.Permissions)
                    : Loc(LocString.PermissionsManyTitle, targets.Count),
                Subtitle = panel.Source.IsRemote
                    ? Loc(LocString.PermissionsOverSftp)
                    : Loc(LocString.PermissionsIn, Paths.Homed(panel.Source, panel.Folder)),
                Key = "Ctrl+X, C",
                Verb = Loc(LocString.PermissionsVerb),
                Weight = Weight.Reversible,
                Items = targets,
                FieldLabel = Loc(LocString.OperationMode),
                Value = current.Length == 0 ? "644" : current,
                FieldHint = Keeping(folders),
                Note = asking => Modes.Read(asking.Value) is null
                    ? new(Loc(LocString.PermissionsWrong), true)
                    : new(Modes.Letters(asking.Value)),
                Confirm = asking => Answers.From(() => Changing(panel, targets, asking.Value),
                    refused =>
                    {
                        State.Output = refused == 0
                            ? Loc(LocString.SaidModeChanged, Counted(targets), asking.Value)
                            : Loc(LocString.SaidModeRefused, refused, targets.Count, asking.Value);

                        panel.Reload();
                    }),
            }));
    }

    /// <summary>
    /// Hands a chown to the shell where the panel is looking. Ownership is the one thing none of the
    /// three protocols carries a request for, and a shell has said <c>chown user:group</c> for fifty
    /// years.
    /// </summary>
    public void Owner()
    {
        var panel = Here;
        var targets = panel.Targets();

        if (Nothing(targets))
        {
            return;
        }

        Dialogs.AskFor(
            Loc(LocString.OwnerTitle),
            Loc(LocString.OperationOwner),
            "",
            Loc(LocString.OwnerVerb),
            owner =>
            {
                var command = new StringBuilder("chown ").Append(owner.Trim());

                foreach (var entry in targets)
                {
                    command.Append(" \"").Append(entry.Name).Append('"');
                }

                _runner.Run(command.ToString(), panel.Folder, panel.Source, panel.Reload);
            });
    }

    /// <summary>What a folder in the list means for a mode, said beside the field.</summary>
    /// <param name="folders">How many of them there are.</param>
    /// <returns>The words.</returns>
    private static string Keeping(int folders) => folders switch
    {
        0 => Loc(LocString.PermissionsDigits),
        1 => Loc(LocString.PermissionsFolderKeeps),
        _ => Loc(LocString.PermissionsFoldersKeep),
    };

    /// <summary>How many of them the source would not change.</summary>
    /// <param name="panel">Where they live.</param>
    /// <param name="targets">Which ones.</param>
    /// <param name="mode">What to set.</param>
    /// <returns>How many were refused.</returns>
    private static async Task<int> Changing(FilePanel panel, IReadOnlyList<FileEntry> targets, string mode)
    {
        var refused = 0;

        foreach (var entry in targets)
        {
            refused += await panel.Source.TryChangeModeAsync(entry, mode, CancellationToken.None)
                .ConfigureAwait(false)
                ? 0
                : 1;
        }

        return refused;
    }

    /// <summary>What was acted on, named when there is one of them and counted when there are more.</summary>
    /// <param name="sources">What was acted on.</param>
    /// <returns>The words.</returns>
    private static string Counted(IReadOnlyList<FileEntry> sources) =>
        sources.Count == 1 ? sources[0].Name : Loc(LocString.SaidItems, sources.Count);
}
