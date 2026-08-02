using System;
using System.Collections.Generic;
using System.Text;
using Arlecchino.Commander.Files;
using Arlecchino.Commander.Stores;
using Arlecchino.State;

namespace Arlecchino.Commander.Views;

/// <summary>
/// The menu behind <c>F2</c>: a file of named shell commands, edited by hand, run against whatever
/// the panels are pointing at. The first time it is opened there is no file, so one is written with a
/// few entries in it to be edited into whatever the work at hand needs.
/// </summary>
public static class Recipes
{
    /// <summary>Opens it.</summary>
    /// <param name="doings">Everything the screen can do, for the panels and the dialog.</param>
    /// <param name="runner">What runs what is chosen.</param>
    /// <param name="state">Where the last word said is kept.</param>
    public static void Open(Doings doings, Runner runner, ArlecchinoState state)
    {
        ArgumentNullException.ThrowIfNull(doings);
        ArgumentNullException.ThrowIfNull(state);

        var entries = UserMenu.Read();

        if (entries.Count == 0)
        {
            state.Output = UserMenu.WriteStarter()
                ? Loc(LocString.SaidMenuWritten, UserMenu.Location)
                : Loc(LocString.SaidNoMenu, UserMenu.Location);

            return;
        }

        var titles = new List<string>(entries.Count);

        foreach (var entry in entries)
        {
            titles.Add(entry.Title);
        }

        doings.Dialogs.Pick(Loc(LocString.Menu), titles, chosen =>
        {
            foreach (var entry in entries)
            {
                if (entry.Title != chosen)
                {
                    continue;
                }

                Run(doings, runner, entry);

                return;
            }
        });
    }

    /// <summary>Runs an entry with what the panels are pointing at put in.</summary>
    /// <param name="doings">Everything the screen can do, for the panels.</param>
    /// <param name="runner">What runs it.</param>
    /// <param name="entry">The entry that was chosen.</param>
    private static void Run(Doings doings, Runner runner, MenuEntry entry)
    {
        var panel = doings.Panels.Active;
        var other = doings.Panels.Passive;
        var marked = new StringBuilder();

        foreach (var target in panel.Targets())
        {
            marked.Append(marked.Length == 0 ? "" : " ").Append(UserMenu.Quoted(target.Name));
        }

        var whole = UserMenu.Whole(
            entry,
            panel.Current?.Name ?? "",
            marked.ToString(),
            panel.Folder,
            other.Folder,
            other.Current?.Name ?? "");

        runner.Run(whole, panel.Folder, panel.Source, panel.Reload);
    }
}
