using System;
using System.Globalization;

namespace Arlecchino.Commander.Model;

public static class Sizes
{
    private const long Kilobyte = 1024;

    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>
    /// A size as it is read aloud: <c>4.1 MB</c>, <c>812 KB</c>, <c>640 B</c>. The unit is spelled out
    /// and stands apart from the number, because nothing in this interface is shortened to fit a cell
    /// budget — the two characters saved by <c>4.1M</c> cost a reader the moment it takes to parse it.
    /// </summary>
    /// <param name="bytes">How many bytes.</param>
    /// <returns>The size, in the largest unit that leaves a number worth reading.</returns>
    public static string Brief(long bytes)
    {
        double size = bytes;
        var unit = 0;

        while (size >= Kilobyte && unit < Units.Length - 1)
        {
            size /= Kilobyte;
            unit++;
        }

        return unit == 0
            ? $"{bytes.ToString(CultureInfo.InvariantCulture)} B"
            : $"{size.ToString(size >= 10 ? "0" : "0.0", CultureInfo.InvariantCulture)} {Units[unit]}";
    }

    /// <summary>
    /// When something happened, told the way a person would: the time for today, <c>yesterday</c> for
    /// yesterday, a day and a month for this year, and the year as well for anything older. A column of
    /// <c>02.08.26 08:52</c> answers a question nobody asked; what is wanted at a glance is how old the
    /// file is, and that is what this says.
    /// </summary>
    /// <param name="moment">When it was.</param>
    /// <returns>The words for it, or nothing when there is no date at all.</returns>
    public static string When(DateTime moment)
    {
        if (moment == default)
        {
            return "";
        }

        var today = DateTime.Now.Date;
        var day = moment.Date;

        if (day == today)
        {
            return moment.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        if (day == today.AddDays(-1))
        {
            return "yesterday";
        }

        return day.Year == today.Year
            ? moment.ToString("d MMM", CultureInfo.InvariantCulture)
            : moment.ToString("MMM yyyy", CultureInfo.InvariantCulture);
    }

    public static string Grouped(long bytes) => bytes.ToString("N0", CultureInfo.InvariantCulture);
}
