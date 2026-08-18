namespace Arlecchino.Commander.Model;

/// <summary>
/// Comparing names with each run of digits read as the number it is, so that <c>file2</c> comes before
/// <c>file10</c>. A run beginning with a zero is compared digit by digit, the shorter padding first.
/// </summary>
public static class NaturalSort
{
    /// <summary>Compares two names, reading runs of digits as numbers.</summary>
    /// <param name="first">One name.</param>
    /// <param name="second">The other.</param>
    /// <returns>Negative, zero or positive, as string comparisons go.</returns>
    public static int Compare(string first, string second)
    {
        var firstAt = 0;
        var secondAt = 0;

        while (true)
        {
            var left = At(first, firstAt);
            var right = At(second, secondAt);

            if (char.IsAsciiDigit(left) && char.IsAsciiDigit(right))
            {
                var padded = left == '0' || right == '0';
                var order = padded
                    ? Padded(first, second, ref firstAt, ref secondAt)
                    : Counted(first, second, ref firstAt, ref secondAt);

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

            firstAt++;
            secondAt++;
        }
    }

    /// <summary>
    /// Two numbers neither of which is padded, so the longer one is the larger one. Only when they run
    /// out together does the first digit they differed at decide it.
    /// </summary>
    /// <param name="first">One name.</param>
    /// <param name="second">The other.</param>
    /// <param name="firstAt">Where the run starts in the first, left at where it ends.</param>
    /// <param name="secondAt">The same for the second.</param>
    /// <returns>Negative, zero or positive.</returns>
    private static int Counted(string first, string second, ref int firstAt, ref int secondAt)
    {
        var bias = 0;

        while (true)
        {
            var left = At(first, firstAt);
            var right = At(second, secondAt);
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

                    firstAt++;
                    secondAt++;

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
    /// <param name="firstAt">Where the run starts in the first, left at where it ends.</param>
    /// <param name="secondAt">The same for the second.</param>
    /// <returns>Negative, zero or positive.</returns>
    private static int Padded(string first, string second, ref int firstAt, ref int secondAt)
    {
        while (true)
        {
            var left = At(first, firstAt);
            var right = At(second, secondAt);
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

                    firstAt++;
                    secondAt++;

                    break;
            }
        }
    }

    private static char At(string text, int index) => index < text.Length ? text[index] : '\0';
}
