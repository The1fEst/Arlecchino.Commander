using System.Collections.Generic;
using Arlecchino.Commander.Files.Disks;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Widgets.Dialogs;
using Arlecchino.Commander.Widgets.Panels;

namespace Arlecchino.Commander.Views.Doing;

/// <summary>
/// The disks of the machine as a list to send a panel to. What is offered is what the machine is
/// holding rather than what it happens to have mounted, so a stick just pushed in is in the list.
/// </summary>
public static class PlaceList
{
    /// <summary>Asks the machine what it has and opens the list on the answer.</summary>
    /// <param name="doings">Everything the screen can do, for the dialog and what is said.</param>
    /// <param name="panel">The panel the chosen place is shown in.</param>
    public static void Open(Doings doings, FilePanel panel)
    {
        Answers.From(
            Disks.Current.ReadAsync,
            places => doings.Dialogs.Pick(
                Loc(LocString.PickDrive),
                Rows(doings, panel, places),
                static _ => { }));
    }

    /// <summary>
    /// One row per place, the label leading and what is known about it said quietly beside. A place not
    /// mounted says so where the room left on it would be, there being no room to report yet.
    /// </summary>
    /// <param name="doings">Everything the screen can do, for what is said.</param>
    /// <param name="panel">The panel the chosen place is shown in.</param>
    /// <param name="places">What the machine is holding.</param>
    /// <returns>The rows.</returns>
    private static List<Pick> Rows(Doings doings, FilePanel panel, IReadOnlyList<Place> places)
    {
        var rows = new List<Pick>(places.Count);

        foreach (var place in places)
        {
            var chosenPlace = place;

            rows.Add(new(place.Label, Said(place), "", () => doings.ShowPlace(panel, chosenPlace)));
        }

        return rows;
    }

    /// <summary>What is said about a place beside its name.</summary>
    /// <param name="place">The place.</param>
    /// <returns>The file system on it, how large it is, and how much of it is left.</returns>
    internal static string Said(Place place)
    {
        var size = place.Size > 0 ? Sizes.Brief(place.Size) : "";
        var room = place.IsMounted ? Disks.Room(place) : Loc(LocString.PlaceNotMounted);
        var measures = size.Length > 0 ? Loc(LocString.Joined, size, room) : room;

        return place.Kind.Length > 0 ? Loc(LocString.Joined, place.Kind, measures) : measures;
    }
}
