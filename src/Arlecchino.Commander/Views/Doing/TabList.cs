using System;
using System.Collections.Generic;
using Arlecchino.Commander.Widgets.Dialogs;

namespace Arlecchino.Commander.Views.Doing;

/// <summary>
/// The tabs as a list rather than as a band along the top.
///
/// The band takes a click on a tab, which is enough while there are three of them and no use at all
/// over ssh from a terminal that reports no mouse. This is the other way in: <c>F2</c> shows the tabs
/// with the two things worth doing to them, and the palette behind <c>Ctrl+K</c> shows the same tabs
/// among everything else, so the tab on a server is reached by typing the name of the server.
/// </summary>
public static class TabList
{
    /// <summary>Opens the list.</summary>
    /// <param name="doings">Everything the screen can do, for the dialog and the tabs.</param>
    public static void Open(Doings doings)
    {
        ArgumentNullException.ThrowIfNull(doings);

        var sessions = doings.Sessions;
        var where = Loc(LocString.TabsTitle).ToLowerInvariant();
        var rows = new List<Pick> { new(Loc(LocString.TabsNew), where, "Alt+T", sessions.Add) };

        if (sessions.All.Count > 1)
        {
            rows.Add(new(Loc(LocString.TabsClose), where, "Alt+W", () => sessions.Close(sessions.Current)));
        }

        rows.AddRange(Rows(doings));

        doings.Dialogs.Pick(Loc(LocString.TabsTitle), rows, static _ => { });
    }

    /// <summary>
    /// One row per tab there is, named the way its own tab in the band is named so the row and the
    /// thing it points at read alike. The number leads the name rather than sitting beside it because
    /// a list narrows on what is written in the row: two tabs on the same pair of folders are told
    /// apart by typing the number, and the tab on a server by typing the server.
    ///
    /// Only the tabs, and not the keys that open and close one: those are commands of the screen, and
    /// the palette lists every one of those already. Adding them here would list them twice.
    /// </summary>
    /// <param name="doings">Everything the screen can do, for the tabs.</param>
    /// <returns>The rows.</returns>
    public static List<Pick> Rows(Doings doings)
    {
        ArgumentNullException.ThrowIfNull(doings);

        var sessions = doings.Sessions;
        var rows = new List<Pick>(sessions.All.Count);
        var where = Loc(LocString.TabsTitle).ToLowerInvariant();

        for (var index = 0; index < sessions.All.Count; index++)
        {
            var which = index;

            rows.Add(new(
                Loc(LocString.Joined, index + 1, sessions.All[index].Label),
                index == sessions.Open.Value ? Loc(LocString.TabsOnScreen) : where,
                "",
                () => sessions.Show(which)));
        }

        return rows;
    }
}
