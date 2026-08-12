using System.Collections.Generic;
using Arlecchino.Atoms.Local;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Rendering;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
///     The bar along the bottom, which says in words what the keys of this moment do. All ten are shown
///     however narrow the terminal is, a label that would not fit being carried onto the next row.
/// </summary>
public sealed class ActionBar
{
    private const int SideRoom = 2;
    private const int Around = 4;

    private readonly Pair _panels;

    /// <summary>Draws the bar under whichever panel is being worked in.</summary>
    /// <param name="panels">The two panels on screen.</param>
    public ActionBar(Pair panels)
    {
        _panels = panels;
    }

    /// <summary>
    ///     How many rows the bar takes, worked out while drawing rather than before. An atom, so the screen
    ///     that leaves room for the bar can lay itself out again on the frame after it changed.
    /// </summary>
    public LocalAtom<int> Height { get; } = new(1);

    /// <summary>
    ///     Draws it, wrapping onto another row whenever the next label would run past the edge. The rows it
    ///     is given are the rows it asked for last time, and the new count goes on <see cref="Height" />.
    /// </summary>
    /// <param name="bar">The rows to draw on.</param>
    public void Draw(SurfaceRegion bar)
    {
        var coat = Skin.Quiet;

        bar.Fill(coat.Text);

        var actions = Actions();
        var column = SideRoom;
        var row = 0;
        Height.Value = 1;

        foreach (var (key, label) in actions)
        {
            var wanted = key.Length + label.Length + Around;
            if (column + wanted > bar.Width - SideRoom)
            {
                column = SideRoom;
                row++;
                Height.Value++;
            }

            bar.Write(row, column, $" {key} ", Skin.Paint(Skin.Sea, Skin.Chip));
            bar.Write(row, column + key.Length + 3, label, coat.Second);

            column += wanted;
        }
    }

    /// <summary>
    ///     The ten function keys, in the order everyone who has used a file manager knows them by. What
    ///     changes is not which keys are on the bar but what they are said to do.
    /// </summary>
    /// <returns>The key and what pressing it would do now.</returns>
    private List<(string Key, string Label)> Actions()
    {
        var panel = _panels.Active;
        var held = panel.Targets().Count;
        var many = panel.State.Marks.Count == 0
            ? ""
            : held == 1
                ? Loc(LocString.BarOneItem)
                : Loc(LocString.BarManyItems, held);

        return
        [
            ("F1", Loc(LocString.BarHelp)),
            ("F2", Loc(LocString.TabsTitle)),
            ("F3", Loc(LocString.View)),
            ("F4", Loc(LocString.Edit)),
            ("F5", Loc(LocString.Copy) + many),
            ("F6", Loc(LocString.Move) + many),
            ("F7", Loc(LocString.NewFolder)),
            ("F8", Loc(LocString.Delete) + many),
            ("F9", Loc(LocString.BarCommands)),
            ("F10", Loc(LocString.BarQuit))
        ];
    }
}
