using System;

namespace Arlecchino.Commander.Model;

/// <summary>
/// Comparing names the way somebody reading them would. Compared letter by letter, <c>file10</c> comes
/// before <c>file2</c>, because <c>1</c> comes before <c>2</c> — which is correct about the characters
/// and wrong about the folder, where the tenth thing belongs after the second. A run of digits is
/// therefore read as the number it is rather than as the characters it is made of.
///
/// Leading zeroes are the one place where that reading has to stop. <c>08</c> and <c>8</c> are the same
/// number under a different name, and a folder of <c>007</c>, <c>08</c>, <c>9</c> is padded on purpose;
/// so a run that begins with a zero is compared digit by digit, where the shorter padding sorts first.
/// </summary>
public static class NaturalSort
{
    /// <summary>Compares two names, reading runs of digits as numbers.</summary>
    /// <param name="first">One name.</param>
    /// <param name="second">The other.</param>
    /// <returns>Negative, zero or positive, as string comparisons go.</returns>
    public static int Compare(string first, string second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var here = 0;
        var there = 0;

        while (true)
        {
            var left = At(first, here);
            var right = At(second, there);

            if (char.IsAsciiDigit(left) && char.IsAsciiDigit(right))
            {
                var padded = left == '0' || right == '0';
                var order = padded
                    ? Padded(first, second, ref here, ref there)
                    : Counted(first, second, ref here, ref there);

                if (order != 0)
                {
                    return order;
                }

                continue;
            }

            if (left == '\0' || right == '\0')
            {
                return left == right ? 0 : left == '\0' ? -1 : 1;
            }

            var one = char.ToUpperInvariant(left);
            var other = char.ToUpperInvariant(right);

            if (one != other)
            {
                return one < other ? -1 : 1;
            }

            here++;
            there++;
        }
    }

    /// <summary>
    /// Two numbers neither of which is padded, so the longer one is the larger one. Only when they run
    /// out together does the first digit they differed at decide it.
    /// </summary>
    /// <param name="first">One name.</param>
    /// <param name="second">The other.</param>
    /// <param name="here">Where the run starts in the first, left at where it ends.</param>
    /// <param name="there">The same for the second.</param>
    /// <returns>Negative, zero or positive.</returns>
    private static int Counted(string first, string second, ref int here, ref int there)
    {
        var bias = 0;

        while (true)
        {
            var left = At(first, here);
            var right = At(second, there);
            var digits = (char.IsAsciiDigit(left), char.IsAsciiDigit(right));

            switch (digits)
            {
                case (true, false):
                    return 1;
                case (false, true):
                    return -1;
                case (false, false):
                    return bias;
                default:
                    if (bias == 0 && left != right)
                    {
                        bias = left < right ? -1 : 1;
                    }

                    here++;
                    there++;

                    break;
            }
        }
    }

    /// <summary>
    /// Two numbers where at least one is padded with zeroes, so the first digit they differ at decides
    /// it and the shorter run comes first.
    /// </summary>
    /// <param name="first">One name.</param>
    /// <param name="second">The other.</param>
    /// <param name="here">Where the run starts in the first, left at where it ends.</param>
    /// <param name="there">The same for the second.</param>
    /// <returns>Negative, zero or positive.</returns>
    private static int Padded(string first, string second, ref int here, ref int there)
    {
        while (true)
        {
            var left = At(first, here);
            var right = At(second, there);
            var digits = (char.IsAsciiDigit(left), char.IsAsciiDigit(right));

            switch (digits)
            {
                case (true, false):
                    return 1;
                case (false, true):
                    return -1;
                case (false, false):
                    return 0;
                default:
                    if (left != right)
                    {
                        return left < right ? -1 : 1;
                    }

                    here++;
                    there++;

                    break;
            }
        }
    }

    private static char At(string text, int index) => index < text.Length ? text[index] : '\0';
}
