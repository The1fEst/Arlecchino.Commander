using System;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Focus;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Layout;
using Arlecchino.Navigation;
using Arlecchino.Rendering;
using static Arlecchino.Layout.PaneSplit;
using static Arlecchino.Layout.PaneTree;

namespace Arlecchino.Commander.Views.Commanding;

/// <summary>
///     Where everything on the screen sits: the two panels side by side with the gutter between them, and
///     the line and the bar of keys under both. It holds the focus that walks the same tree.
/// </summary>
public sealed class CommanderLayout : IDisposable
{
    private readonly CommanderFooter _footer;
    private readonly Gutter _gutter;
    private readonly ArlecchinoKeymap _keymap;
    private readonly Pair _panels;
    private readonly IDisposable[] _resized;
    private FocusRing _focus;

    private PaneTree _tree;

    /// <summary>Lays the screen out over the panels it is given.</summary>
    /// <param name="panels">The two panels on screen.</param>
    /// <param name="sessions">Which side is being worked in, which the gutter points at.</param>
    /// <param name="footer">The lines under the panels, which are left room for.</param>
    /// <param name="keymap">Keys the focus steps by.</param>
    public CommanderLayout(Pair panels, Sessions sessions, CommanderFooter footer, ArlecchinoKeymap keymap)
    {
        _panels = panels;
        _footer = footer;
        _keymap = keymap;
        _gutter = new(sessions, panels);

        _resized = footer.WhenResized(() => _tree = Lay());
        _tree = Lay();
        _focus = _tree.AsFocusRing(keymap);
    }

    /// <summary>Gives up what watching the heights of the bar and the two lines took out.</summary>
    public void Dispose()
    {
        foreach (var resized in _resized)
        {
            resized.Dispose();
        }
    }

    /// <summary>
    ///     Lays the screen out again over the panels the pair holds now, which a tab that has been stepped to
    ///     put there. The focus ring walks the tree, so it is made anew with it.
    /// </summary>
    public void Rebuild()
    {
        _tree = Lay();
        _focus = _tree.AsFocusRing(_keymap);
    }

    /// <summary>Gives the keyboard to one of the panels.</summary>
    /// <param name="panel">Which one.</param>
    public void Focus(FilePanel panel)
    {
        _focus.Focus(panel);
    }

    /// <summary>Draws everything the tree holds.</summary>
    /// <param name="screen">What is drawn on.</param>
    public void Draw(SurfaceRegion screen)
    {
        _tree.Draw(screen);
    }

    /// <summary>Offers the key to whatever has the keyboard.</summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns>Where whatever took it wants to go.</returns>
    public ViewRoute Handle(KeyPress key)
    {
        return _focus.Handle(key);
    }

    /// <summary>Offers the click to whatever was clicked in.</summary>
    /// <param name="mouse">The event that arrived.</param>
    /// <returns>Where whatever took it wants to go.</returns>
    public ViewRoute HandleMouse(MouseEvent mouse)
    {
        return _focus.HandleMouse(mouse);
    }

    /// <summary>Both splits are measured from the end, so the foot keeps its rows as the screen grows.</summary>
    /// <returns>The tree, which the focus ring is made from as well as drawn.</returns>
    private PaneTree Lay()
    {
        return Branch(
            Rows,
            PaneSize.CellsFromEnd(_footer.Rows),
            Branch(
                Columns,
                PaneSize.Fraction(0.5),
                Leaf(_panels.Left),
                Branch(
                    Columns,
                    PaneSize.Cells(Gutter.Width),
                    Leaf(_gutter.Draw),
                    Leaf(_panels.Right))),
            Branch(
                Rows,
                PaneSize.CellsFromEnd(_footer.BarRows),
                Leaf(_footer.DrawLine),
                Leaf(_footer.DrawBar)));
    }
}
