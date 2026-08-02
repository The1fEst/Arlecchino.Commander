using System;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Widgets.Panels;

/// <summary>
/// How the three columns of a panel are laid out. The heads, the rows and a click on a head all have
/// to agree about where each column starts, and they agree by asking here rather than by each doing
/// the same arithmetic.
/// </summary>
public static class PanelColumns
{
    /// <summary>How wide the size column is drawn.</summary>
    public const int SizeWidth = 9;

    /// <summary>How wide the date column is drawn.</summary>
    public const int StampWidth = 11;

    /// <summary>How much space is left between two columns.</summary>
    public const int Gap = 2;

    private const int MinimumName = 12;

    /// <summary>
    /// How wide the three columns come out. The name takes what is left, and on a panel too narrow for
    /// all three the date goes first and the size after it — a name with nothing beside it still says
    /// which file it is.
    /// </summary>
    /// <param name="width">The room there is.</param>
    /// <returns>The width of each column; nought for one that does not fit.</returns>
    public static (int Name, int Size, int Date) Widths(int width)
    {
        var left = width - Kinds.TagWidth;

        if (left >= MinimumName + Gap + SizeWidth + Gap + StampWidth)
        {
            return (left - Gap - SizeWidth - Gap - StampWidth, SizeWidth, StampWidth);
        }

        return left >= MinimumName + Gap + SizeWidth
            ? (left - Gap - SizeWidth, SizeWidth, 0)
            : (Math.Max(0, left), 0, 0);
    }

    /// <summary>Which column a click along the heads landed on.</summary>
    /// <param name="column">How far along the heads the click was.</param>
    /// <param name="width">How wide the heads are.</param>
    /// <returns>What it sorts by, or nothing when it hit the space between them.</returns>
    public static Sorting? Hit(int column, int width)
    {
        var (name, size, date) = Widths(width);

        if (column < Kinds.TagWidth + name)
        {
            return Sorting.Name;
        }

        if (size > 0 && column < Kinds.TagWidth + name + Gap + size)
        {
            return Sorting.Size;
        }

        return date > 0 ? Sorting.Modified : null;
    }
}
