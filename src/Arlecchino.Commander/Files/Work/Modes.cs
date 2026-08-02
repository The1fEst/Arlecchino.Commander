using System;
using System.Globalization;
using System.IO;

namespace Arlecchino.Commander.Files.Work;

/// <summary>
/// Permissions as a chmod writes them. The three sources keep them in three shapes — a
/// <see cref="UnixFileMode"/> on a disk, nine flags over SFTP, a number over FTP — and all three are
/// the same three octal digits to whoever is typing them.
/// </summary>
public static class Modes
{
    private const int Digits = 8;

    /// <summary>Reads the digits, as typed, into the number underneath.</summary>
    /// <param name="mode">Three or four octal digits.</param>
    /// <returns>The number, or <c>null</c> when that is not what was typed.</returns>
    public static int? Read(string mode)
    {
        ArgumentNullException.ThrowIfNull(mode);

        var trimmed = mode.Trim();

        if (trimmed.Length is 0 or > 4)
        {
            return null;
        }

        var value = 0;

        foreach (var digit in trimmed)
        {
            if (digit is < '0' or > '7')
            {
                return null;
            }

            value = (value * Digits) + (digit - '0');
        }

        return value;
    }

    /// <summary>
    /// The digits themselves as a number: <c>644</c> for <c>644</c>, not the 420 those digits stand
    /// for. Both SFTP and FTP take a mode in this shape, spelling it out again on the far side.
    /// </summary>
    /// <param name="mode">Three or four octal digits.</param>
    /// <returns>The digits as a number, or <c>null</c> when that is not what was typed.</returns>
    public static int? AsDigits(string mode) =>
        Read(mode) is null ? null : int.Parse(mode.Trim(), CultureInfo.InvariantCulture);

    public static string Write(int mode) => Convert.ToString(mode, Digits).PadLeft(3, '0');

    /// <summary>
    /// The digits written out as <c>rwxr-xr-x</c>. Three digits are the compact way of saying it and
    /// the letters are the readable one; a dialog that changes permissions should say both, since
    /// hardly anybody reads <c>750</c> without pausing.
    /// </summary>
    /// <param name="mode">Three or four octal digits, as typed.</param>
    /// <returns>The nine letters, or nothing when the digits were not digits.</returns>
    public static string Letters(string mode)
    {
        if (Read(mode) is not { } value)
        {
            return "";
        }

        var letters = new char[9];

        for (var third = 0; third < 3; third++)
        {
            var bits = (value >> ((2 - third) * 3)) & 7;

            letters[third * 3] = (bits & 4) != 0 ? 'r' : '-';
            letters[(third * 3) + 1] = (bits & 2) != 0 ? 'w' : '-';
            letters[(third * 3) + 2] = (bits & 1) != 0 ? 'x' : '-';
        }

        return new(letters);
    }

    /// <summary>
    /// The same number as a <see cref="UnixFileMode"/>, which carries the standard bits in the
    /// standard places and so needs no translating.
    /// </summary>
    /// <param name="mode">The number.</param>
    /// <returns>The mode.</returns>
    public static UnixFileMode AsUnix(int mode) => (UnixFileMode)mode;

    public static int FromUnix(UnixFileMode mode) => (int)mode;
}
