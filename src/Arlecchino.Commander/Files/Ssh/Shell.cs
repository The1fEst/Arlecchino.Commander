using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Arlecchino.Commander.Files.Ssh;

/// <summary>
/// One shell dialect: how a folder is removed, how a command is run somewhere in particular, and how a
/// command line is handed over. The shell at either end of a connection is the same thing here.
/// </summary>
public abstract class Shell
{
    /// <summary>
    /// The command that removes a folder and everything under it.
    /// </summary>
    /// <param name="path">The folder, as SFTP spells it.</param>
    /// <returns>The command, or <c>null</c> when this shell has no one-line way to do it.</returns>
    public abstract string? Sweep(string path);

    /// <summary>
    /// A command run where the panel is looking. A command line that says <c>ls</c> means the folder
    /// on screen, not whatever the server drops a session into.
    /// </summary>
    /// <param name="folder">The folder to run it in, as SFTP spells it.</param>
    /// <param name="command">What the user typed.</param>
    /// <returns>The command to send.</returns>
    public abstract string Within(string folder, string command);

    /// <summary>
    /// The command that makes a hard link. SFTP has a request of its own for a symbolic link and
    /// none for this one, so it is asked of the shell — and the shells do not agree on its name.
    /// </summary>
    /// <param name="path">Where the link goes.</param>
    /// <param name="target">What it points at.</param>
    /// <returns>The command, or <c>null</c> when this shell has no way to make one.</returns>
    public abstract string? Link(string path, string target);

    /// <summary>Wraps a path so that this shell reads it back as one word, whatever is in it.</summary>
    /// <param name="path">The path, as SFTP spells it.</param>
    /// <returns>The path, quoted.</returns>
    public abstract string Quote(string path);

    /// <summary>
    /// Fills in what starts this shell with a command to run: the executable and the arguments that
    /// carry the command line to it unchanged.
    /// </summary>
    /// <param name="started">What to fill in.</param>
    /// <param name="command">What the user typed.</param>
    public abstract void Hand(ProcessStartInfo started, string command);

    /// <summary>
    /// Works out what is answering on the far side of a connection, in as few round trips as it can:
    /// a POSIX shell owns up to <c>uname</c>, PowerShell to its own version table, and
    /// <c>cmd.exe</c> to its comspec.
    /// </summary>
    /// <param name="run">Runs a command and answers with its output and exit status.</param>
    /// <returns>The shell that answered, or <see cref="ForeignShell"/> when none of them did.</returns>
    public static async Task<Shell> AskAsync(Func<string, Task<(string Output, int Status)>> run)
    {
        var unix = await run("uname -s").ConfigureAwait(false);

        if (unix.Status == 0 && unix.Output.Trim().Length > 0 && !Confused(unix.Output))
        {
            return PosixShell.Instance;
        }

        var powershell = await run("$PSVersionTable.PSEdition").ConfigureAwait(false);

        if (powershell.Output.Contains("Core", StringComparison.Ordinal) ||
            powershell.Output.Contains("Desktop", StringComparison.Ordinal))
        {
            return PowerShellShell.Instance;
        }

        var comspec = await run("echo %COMSPEC%").ConfigureAwait(false);

        return comspec.Output.Contains("cmd.exe", StringComparison.OrdinalIgnoreCase)
            ? WindowsCommandShell.Instance
            : ForeignShell.Instance;
    }

    private static bool Confused(string output) =>
        output.Contains("not recognized", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("CommandNotFound", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("not found", StringComparison.OrdinalIgnoreCase);
}
