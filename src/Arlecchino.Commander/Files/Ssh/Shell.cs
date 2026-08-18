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

/// <summary>A POSIX shell: Linux, macOS, BSD, anything with <c>rm</c>.</summary>
public sealed class PosixShell : Shell
{
    /// <summary>The one of these there is.</summary>
    public static readonly PosixShell Instance = new();

    /// <summary>
    /// Removes the folder without <c>-f</c>. That flag walks past a read-only file, which is often the last
    /// thing standing between a deletion and something that should not have gone with it, and it answers
    /// nought whether anything was there at all.
    /// </summary>
    /// <param name="path">The folder, as SFTP spells it.</param>
    /// <returns>The command.</returns>
    public override string Sweep(string path) => $"rm -r -- {Quote(path)}";

    /// <inheritdoc/>
    public override string Link(string path, string target) => $"ln {Quote(target)} {Quote(path)}";

    /// <inheritdoc/>
    public override string Within(string folder, string command) => $"cd {Quote(folder)} && {command}";

    /// <inheritdoc/>
    public override void Hand(ProcessStartInfo started, string command)
    {
        started.FileName = Environment.GetEnvironmentVariable("SHELL") is { Length: > 0 } shell
            ? shell
            : "/bin/sh";

        started.ArgumentList.Add("-c");
        started.ArgumentList.Add(command);
    }

    /// <inheritdoc/>
    public override string Quote(string path) =>
        $"'{path.Replace("'", @"'\''", StringComparison.Ordinal)}'";
}

/// <summary>
/// What the two Windows shells agree on, which is the spelling of a path and nothing else. SFTP
/// reports one as <c>/C:/Users/…</c>, and neither <c>cmd.exe</c> nor PowerShell will take that as it
/// stands.
/// </summary>
public abstract class WindowsShell : Shell
{
    /// <summary>The Windows spelling of a path SFTP reports with a leading slash.</summary>
    /// <param name="path">The path as SFTP spells it.</param>
    /// <returns>The same path with a drive letter and backslashes.</returns>
    protected static string Local(string path)
    {
        var trimmedText = path.StartsWith('/') && path.Length > 2 && path[2] == ':' ? path[1..] : path;

        return trimmedText.Replace('/', '\\');
    }
}

/// <summary>Windows <c>cmd.exe</c>, the stock default shell of OpenSSH on Windows.</summary>
public sealed class WindowsCommandShell : WindowsShell
{
    /// <summary>The one of these there is.</summary>
    public static readonly WindowsCommandShell Instance = new();

    /// <summary>
    /// Answers that there is no one-line way, so the tree is walked instead. <c>rmdir /s</c> takes read-only
    /// files along with the rest, and exits nought even where it failed.
    /// </summary>
    /// <param name="path">The folder, as SFTP spells it.</param>
    /// <returns><c>null</c>, always.</returns>
    public override string? Sweep(string path) => null;

    /// <inheritdoc/>
    public override string Link(string path, string target) => $"mklink /h {Quote(path)} {Quote(target)}";

    /// <inheritdoc/>
    public override string Within(string folder, string command) => $"cd /d {Quote(folder)} && {command}";

    /// <inheritdoc/>
    public override string Quote(string path) => $"\"{Local(path)}\"";

    /// <summary>
    /// Fills in <c>cmd.exe</c> behind <c>/s /c</c>, which is the only spelling it reads back unchanged. The
    /// arguments go over as one raw string, since an escaped one loses a quote.
    /// </summary>
    /// <param name="started">What to fill in.</param>
    /// <param name="command">What the user typed.</param>
    public override void Hand(ProcessStartInfo started, string command)
    {
        started.FileName = "cmd.exe";
        started.Arguments = $"/s /c \"{command}\"";
    }
}

/// <summary>Windows PowerShell or PowerShell Core, set as the default shell.</summary>
public sealed class PowerShellShell : WindowsShell
{
    /// <summary>The one of these there is.</summary>
    public static readonly PowerShellShell Instance = new();

    /// <summary>
    /// Removes the folder without <c>-Force</c>, which would take read-only and hidden items too.
    /// <c>-Recurse</c> stops the prompt about a folder that holds things.
    /// </summary>
    /// <param name="path">The folder, as SFTP spells it.</param>
    /// <returns>The command.</returns>
    public override string Sweep(string path) =>
        $"Remove-Item -LiteralPath {Quote(path)} -Recurse";

    /// <inheritdoc/>
    public override string Link(string path, string target) =>
        $"New-Item -ItemType HardLink -Path {Quote(path)} -Target {Quote(target)}";

    /// <inheritdoc/>
    public override string Within(string folder, string command) =>
        $"Set-Location -LiteralPath {Quote(folder)}; {command}";

    /// <inheritdoc/>
    public override void Hand(ProcessStartInfo started, string command)
    {
        started.FileName = "powershell.exe";
        started.ArgumentList.Add("-NoProfile");
        started.ArgumentList.Add("-Command");
        started.ArgumentList.Add(command);
    }

    /// <inheritdoc/>
    public override string Quote(string path) =>
        $"'{Local(path).Replace("'", "''", StringComparison.Ordinal)}'";
}

/// <summary>
/// Something none of the others, or a server that would not answer. It takes no shortcuts: a command goes
/// over as it was typed, and a folder is removed the long way, one entry at a time.
/// </summary>
public sealed class ForeignShell : Shell
{
    /// <summary>The one of these there is.</summary>
    public static readonly ForeignShell Instance = new();

    /// <inheritdoc/>
    public override string? Sweep(string path) => null;

    /// <inheritdoc/>
    public override string? Link(string path, string target) => null;

    /// <inheritdoc/>
    public override string Within(string folder, string command) => command;

    /// <inheritdoc/>
    public override string Quote(string path) => path;

    /// <inheritdoc/>
    public override void Hand(ProcessStartInfo started, string command) =>
        PosixShell.Instance.Hand(started, command);
}
