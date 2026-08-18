using System.Collections.Generic;
using Arlecchino.Commander.Widgets.Dialogs;

namespace Arlecchino.Commander.Views.Doing;

/// <summary>
/// The tabs as a list rather than as a band along the top, which is the way in from a terminal that reports
/// no mouse. <c>F2</c> shows them with the two things worth doing to them.
/// </summary>
public static class TabList
{
    /// <summary>Opens the list.</summary>
    /// <param name="doings">Everything the screen can do, for the dialog and the tabs.</param>
    public static void Open(Doings doings)
    {
        var sessions = doings.Sessions;
        var group = Loc(LocString.TabsTitle).ToLowerInvariant();
        var rows = new List<Pick> { new(Loc(LocString.TabsNew), group, "Alt+T", sessions.Add) };

        if (sessions.All.Count > 1)
        {
            rows.Add(new(Loc(LocString.TabsClose), group, "Alt+W", () => sessions.Close(sessions.Current)));
        }

        rows.AddRange(Rows(doings));

        doings.Dialogs.Pick(Loc(LocString.TabsTitle), rows, static _ => { });
    }

    /// <summary>
    /// One row per tab, named the way its own tab in the band is named. The number leads the name, so two
    /// tabs on the same pair of folders are told apart by typing it.
    /// </summary>
    /// <param name="doings">Everything the screen can do, for the tabs.</param>
    /// <returns>The rows.</returns>
    public static List<Pick> Rows(Doings doings)
    {
        var sessions = doings.Sessions;
        var rows = new List<Pick>(sessions.All.Count);
        var group = Loc(LocString.TabsTitle).ToLowerInvariant();

        for (var index = 0; index < sessions.All.Count; index++)
        {
            var choice = index;

            rows.Add(new(
                Loc(LocString.Joined, index + 1, sessions.All[index].Label),
                index == sessions.Open.Value ? Loc(LocString.TabsOnScreen) : group,
                "",
                () => sessions.Show(choice)));
        }

        return rows;
    }
}
