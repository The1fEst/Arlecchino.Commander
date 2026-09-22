using System.Diagnostics;

namespace Arlecchino.Commander.Files.Opening;

/// <summary>
/// What Windows opens a file with, which is no program at all. The association lives in the registry,
/// and only the shell reads it.
/// </summary>
public sealed class WindowsOpener : ProcessOpener
{
    /// <summary>The one of these there is.</summary>
    public static readonly WindowsOpener Instance = new();

    /// <summary>
    /// The file stands where a program would, which is what asks the shell to look the association up.
    /// Nothing is redirected: the window it reaches has no streams of ours to write on.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The start, which names the file rather than a handler.</returns>
    public override ProcessStartInfo Starting(string path) => new(path) { UseShellExecute = true };
}
