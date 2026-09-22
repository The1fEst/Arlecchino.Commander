using System.Diagnostics;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Files.Archives;

/// <summary>
/// The program every format the runtime cannot read is handed to, and the one thing that can write into
/// an archive. It is the same program on all three platforms, under whichever of its names is installed.
/// </summary>
public static class SevenZip
{
    /// <summary>
    /// The names it is installed under, in the order they are tried. The first is what the project
    /// ships its own build as, and the second what a distribution packages it as.
    /// </summary>
    private static readonly string[] Names = ["7zz", "7z", "7za"];

    /// <summary>What it is called on this machine, or an empty string when it is not on it.</summary>
    public static string Program { get; } = Found();

    /// <summary>Whether the program is here at all, without which none of this is offered.</summary>
    public static bool Works => Program.Length > 0;

    /// <summary>
    /// A run of the program, with everything it says on its own account kept off the terminal. Its
    /// progress goes to the standard output and its complaints to the error, and both are read.
    /// </summary>
    /// <param name="words">What to run it with.</param>
    /// <returns>The start, ready to run.</returns>
    public static ProcessStartInfo Starting(params string[] words)
    {
        var start = new ProcessStartInfo(Program)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };

        foreach (var word in words)
        {
            start.ArgumentList.Add(word);
        }

        return start;
    }

    private static string Found()
    {
        foreach (var name in Names)
        {
            if (Programs.Present([name]).Count > 0)
            {
                return name;
            }
        }

        return "";
    }
}
