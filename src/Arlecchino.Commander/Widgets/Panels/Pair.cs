namespace Arlecchino.Commander.Widgets.Panels;

/// <summary>
/// The two panels on screen, and which of them is being worked in, so that everything acting on a file can
/// say "this panel" and "the other one". The screen swaps what is in it when the tab changes.
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
