using System;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// The same thing asked of whichever shell the machine has. A terminal of the application's own making is
/// made on more than one kind of machine, and a test that speaks one dialect only ever tests the one.
/// </summary>
internal static class Asked
{
    /// <summary>Prints a word and ends.</summary>
    /// <param name="word">The word.</param>
    /// <returns>The command.</returns>
    internal static string Prints(string word) => Windows ? $"echo {word}" : $"printf '{word}\\n'";

    /// <summary>Ends with the outcome given and prints nothing.</summary>
    /// <param name="outcome">What to exit with.</param>
    /// <returns>The command.</returns>
    internal static string EndsWith(int outcome) => $"exit {outcome}";

    /// <summary>Says so when it is at a terminal, and says nothing at all when it is on a pipe.</summary>
    internal static string AtATerminal => Windows
        ? Through("if (-not [Console]::IsOutputRedirected -and -not [Console]::IsInputRedirected) { 'at a terminal' }")
        : "test -t 0 && test -t 1 && echo at a terminal";

    /// <summary>Prints how large its window is, the rows first and the columns after them.</summary>
    internal static string TheWindow => Windows
        ? Through("$size = $Host.UI.RawUI.WindowSize; Write-Host ('{0} {1}' -f $size.Height, $size.Width)")
        : "stty size";

    /// <summary>Prints the folder it is running in.</summary>
    internal static string TheFolder => Windows ? "cd" : "pwd";

    /// <summary>Waits, and goes on waiting until something stops it.</summary>
    internal static string Waits => Windows ? "pause" : "sleep 30";

    /// <summary>Reads a line and prints it back inside brackets, with a word in front of it.</summary>
    internal static string Repeats => Windows
        ? "cmd /v:on /c \"set /p given=&echo got [!given!]\""
        : "read given; printf 'got [%s]\\n' \"$given\"";

    /// <summary>Asks something, waits to be told, and prints back what it was told.</summary>
    internal static string Asks => Windows
        ? "cmd /v:on /c \"set /p given=password:&echo got !given!\""
        : "printf 'password:'; read given; printf 'got %s\\n' \"$given\"";

    /// <summary>Prints a line in color, which is not asking for the screen.</summary>
    internal static string Colors => Windows
        ? Through("Write-Host -ForegroundColor Green green")
        : "printf '\\033[32mgreen\\033[0m\\n'";

    /// <summary>Prints a line, swaps onto the second screen a terminal keeps, and holds it.</summary>
    internal static string Draws => Windows
        ? Through("Write-Host before; [Console]::Write([char]27 + '[?1049h'); Start-Sleep 30")
        : "printf 'before\\n'; printf '\\033[?1049h'; sleep 30";

    private static bool Windows => OperatingSystem.IsWindows();

    /// <summary>
    /// Asked of PowerShell rather than of the shell itself, for the questions <c>cmd.exe</c> has no words
    /// for at all. It is on every machine of this kind, which the shells people install are not.
    /// </summary>
    /// <param name="command">What to ask it.</param>
    /// <returns>The command.</returns>
    private static string Through(string command) => $"powershell -NoProfile -c \"{command}\"";
}
