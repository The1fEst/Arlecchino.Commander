using System;
using Arlecchino.Atoms.Local;
using Arlecchino.Commander.Stores;
using Arlecchino.Rendering;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
///     The bar along the bottom, which says in words what the keys of this moment do. All ten are shown
///     however narrow the terminal is, a label that would not fit being carried onto the next row.
/// </summary>
public sealed class ActionBar
{
    private const int SideRoom = 2;
    private const int Padding = 4;

    private readonly Keyboard _keyboard;

    /// <summary>Draws the bar from the key table, so that it says what the keys were bound to say.</summary>
    /// <param name="keyboard">Every key the screen answers to.</param>
    public ActionBar(Keyboard keyboard)
    {
        _keyboard = keyboard;
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

        var actions = _keyboard.Keys;
        var column = SideRoom;
        var row = 0;
        Height.Value = 1;

        foreach (var action in actions)
        {
            if (action.Binding.Key is < ConsoleKey.F1 or > ConsoleKey.F12 || action.Binding.Modifiers != 0)
            {
                continue;
            }

            var key = action.Binding.Key.ToString();
            var label = action.Label();
            var target = key.Length + label.Length + Padding;
            if (column + target > bar.Width - SideRoom)
            {
                column = SideRoom;
                row++;
                Height.Value++;
            }

            bar.Write(row, column, $" {key} ", Skin.On(Skin.Chip).Remote);
            bar.Write(row, column + key.Length + 3, label, coat.Second);

            column += target;
        }
    }
}
