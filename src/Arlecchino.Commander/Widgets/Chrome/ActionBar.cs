using System.Collections.Generic;
using Arlecchino.Rendering;

using Arlecchino.Commander.Widgets.Panel;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// The bar along the bottom, which says in words what the keys of this moment do. It is not a fixed
/// ten-slot row: what is on it follows from what is marked and where the panel is looking, and
/// everything it leaves out is behind the palette.
/// </summary>
public sealed class ActionBar
{
    /// <summary>How many rows it takes.</summary>
    public const int Height = 1;

    private const int SideRoom = 2;
    private const int Around = 4;

    private readonly Pair _panels;

    /// <summary>Draws the bar under whichever panel is being worked in.</summary>
    /// <param name="panels">The two panels on screen.</param>
    public ActionBar(Pair panels) => _panels = panels;

    /// <summary>Draws it.</summary>
    /// <param name="bar">The row to draw on.</param>
    public void Draw(SurfaceRegion bar)
    {
        var coat = Skin.Lively;

        bar.Fill(coat.Text);

        var actions = Actions();
        var column = SideRoom;

        if (Room(actions) > bar.Width - (SideRoom * 2))
        {
            actions = Actions(plain: true);
        }

        foreach (var (key, label) in actions)
        {
            var wanted = key.Length + label.Length + Around;

            if (column + wanted > bar.Width - SideRoom)
            {
                break;
            }

            bar.Write(0, column, $" {key} ", Skin.Paint(Skin.Sea, Skin.Chip));
            bar.Write(0, column + key.Length + 3, label, coat.Second);

            column += wanted;
        }
    }

    /// <summary>
    /// The ten function keys, in the order everyone who has used a file manager knows them by. What
    /// changes is not which keys are on the bar but what they are said to do: with three files marked,
    /// <c>F5</c> is no longer "copy" but "copy 3 items", and the bar has answered the question before
    /// it was asked.
    /// </summary>
    /// <param name="plain">
    /// Leaves off what is being acted on, for a terminal too narrow to say it. Ten keys that fit is
    /// worth more than eight that are spelled out and two that fell off the end.
    /// </param>
    /// <returns>The key and what pressing it would do now.</returns>
    private List<(string Key, string Label)> Actions(bool plain = false)
    {
        var panel = _panels.Active;
        var held = panel.Targets().Count;
        var many = plain || panel.State.Marks.Count == 0
            ? ""
            : held == 1 ? Loc(LocString.BarOneItem) : Loc(LocString.BarManyItems, held);

        return
        [
            ("F1", Loc(LocString.BarHelp)),
            ("F2", Loc(LocString.Menu)),
            ("F3", Loc(LocString.View)),
            ("F4", Loc(LocString.Filter)),
            ("F5", Loc(LocString.Copy) + many),
            ("F6", Loc(LocString.Move) + many),
            ("F7", plain ? Loc(LocString.BarMkdir) : Loc(LocString.NewFolder)),
            ("F8", Loc(LocString.Delete) + many),
            ("F9", Loc(LocString.BarCommands)),
            ("F10", Loc(LocString.BarQuit)),
        ];
    }

    /// <summary>How much of the bar a set of actions would take.</summary>
    /// <param name="actions">What would go on it.</param>
    /// <returns>The width they need.</returns>
    private static int Room(List<(string Key, string Label)> actions)
    {
        var wanted = 0;

        foreach (var (key, label) in actions)
        {
            wanted += key.Length + label.Length + Around;
        }

        return wanted;
    }
}
