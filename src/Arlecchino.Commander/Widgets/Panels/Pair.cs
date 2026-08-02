namespace Arlecchino.Commander.Widgets.Panels;

/// <summary>
/// The two panels on screen, and which of them is being worked in.
///
/// Everything that acts on a file acts on "this panel" and "the other one" rather than on the left
/// and the right, and every one of them used to be handed a pair of delegates to find out which was
/// which. They are handed this instead: it is one object, the screen swaps what is in it when the tab
/// changes, and nothing holding it is left pointing at the panels of a tab that is no longer showing.
/// </summary>
public sealed class Pair
{
    /// <summary>Sets the pair up over two panels.</summary>
    /// <param name="left">The panel on the left.</param>
    /// <param name="right">The panel on the right.</param>
    public Pair(FilePanel left, FilePanel right)
    {
        Left = left;
        Right = right;
    }

    /// <summary>The panel on the left.</summary>
    public FilePanel Left { get; private set; }

    /// <summary>The panel on the right.</summary>
    public FilePanel Right { get; private set; }

    /// <summary>The panel being worked in.</summary>
    public FilePanel Active => Right.IsFocused ? Right : Left;

    /// <summary>The other one, which is where most of what is done to a file is going.</summary>
    public FilePanel Passive => Right.IsFocused ? Left : Right;

    /// <summary>Puts another tab's panels in, when the tab has changed.</summary>
    /// <param name="left">The panel on the left.</param>
    /// <param name="right">The panel on the right.</param>
    public void Show(FilePanel left, FilePanel right)
    {
        Left = left;
        Right = right;
    }
}
