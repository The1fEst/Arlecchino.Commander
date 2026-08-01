using System;
using System.Diagnostics;

namespace Arlecchino.Commander.Files;

/// <summary>
/// One shell dialect, and everything that follows from it: how a folder is removed, how a command is
/// run somewhere in particular, and how a command line is handed over at all. The three disagree
/// about all of it — the spelling of a path, the escaping of a quote, the name of the command — so
/// each one writes its own and none of them knows the others exist.
///
/// Which side of a connection it is on makes no difference. The machine this runs on has a shell and
/// so does the far end of an SSH session; they are told apart differently — <see cref="Shells.Local"/>
/// against <see cref="Ask"/> — and after that they are the same thing.
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
    protected abstract string Quote(string path);

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
    public static Shell Ask(Func<string, (string Output, int Status)> run)
    {
        ArgumentNullException.ThrowIfNull(run);

        var unix = run("uname -s");

        if (unix.Status == 0 && unix.Output.Trim().Length > 0 && !Confused(unix.Output))
        {
            return PosixShell.Instance;
        }

        var powershell = run("$PSVersionTable.PSEdition");

        if (powershell.Output.Contains("Core", StringComparison.Ordinal) ||
            powershell.Output.Contains("Desktop", StringComparison.Ordinal))
        {
            return PowerShellShell.Instance;
        }

        var comspec = run("echo %COMSPEC%");

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
    /// Removes the folder without <c>-f</c>. That flag walks past a read-only file, which is often
    /// the last thing standing between a delete and something that should not have gone with it, and
    /// it answers nought whether or not there was anything there at all.
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
        ArgumentNullException.ThrowIfNull(started);

        started.FileName = Environment.GetEnvironmentVariable("SHELL") is { Length: > 0 } shell
            ? shell
            : "/bin/sh";

        started.ArgumentList.Add("-c");
        started.ArgumentList.Add(command);
    }

    /// <inheritdoc/>
    protected override string Quote(string path) =>
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
        ArgumentNullException.ThrowIfNull(path);

        var trimmed = path.StartsWith('/') && path.Length > 2 && path[2] == ':' ? path[1..] : path;

        return trimmed.Replace('/', '\\');
    }
}

/// <summary>Windows <c>cmd.exe</c>, the stock default shell of OpenSSH on Windows.</summary>
public sealed class WindowsCommandShell : WindowsShell
{
    /// <summary>The one of these there is.</summary>
    public static readonly WindowsCommandShell Instance = new();

    /// <summary>
    /// Answers that there is no one-line way, so the tree is walked instead. <c>rmdir /s</c> takes a
    /// read-only file with the rest and reports success, and <c>cmd.exe</c> has no switch that stops
    /// it — the flag to leave off is not <c>/q</c>, which only answers the question it would ask.
    ///
    /// Walking costs a round trip per entry where one command would have done. That is the price of a
    /// delete that stops at what it is not allowed to remove, and it is worth paying.
    /// </summary>
    /// <param name="path">The folder, as SFTP spells it.</param>
    /// <returns><c>null</c>, always.</returns>
    public override string? Sweep(string path) => null;

    /// <inheritdoc/>
    public override string Link(string path, string target) => $"mklink /h {Quote(path)} {Quote(target)}";

    /// <inheritdoc/>
    public override string Within(string folder, string command) => $"cd /d {Quote(folder)} && {command}";

    /// <inheritdoc/>
    protected override string Quote(string path) => $"\"{Local(path)}\"";

    /// <summary>
    /// Fills in <c>cmd.exe</c> behind <c>/s /c</c>, which is the only spelling it reads back
    /// unchanged. Handed the same command as an escaped argument it eats a quote and leaves a path in
    /// half, which is why the arguments go over as one raw string.
    /// </summary>
    /// <param name="started">What to fill in.</param>
    /// <param name="command">What the user typed.</param>
    public override void Hand(ProcessStartInfo started, string command)
    {
        ArgumentNullException.ThrowIfNull(started);

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
    /// Removes the folder without <c>-Force</c>. That switch takes read-only and hidden items too,
    /// which is how a delete reaches further than it was asked to. <c>-Recurse</c> is what stops the
    /// prompt about a folder that holds things, so nothing is left waiting on an answer.
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
        ArgumentNullException.ThrowIfNull(started);

        started.FileName = "powershell.exe";
        started.ArgumentList.Add("-NoProfile");
        started.ArgumentList.Add("-Command");
        started.ArgumentList.Add(command);
    }

    /// <inheritdoc/>
    protected override string Quote(string path) =>
        $"'{Local(path).Replace("'", "''", StringComparison.Ordinal)}'";
}

/// <summary>
/// Something none of the others, or a server that would not answer. It takes no shortcuts: a command
/// goes over as it was typed and a folder is removed the long way, one entry at a time.
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
    protected override string Quote(string path) => path;

    /// <inheritdoc/>
    public override void Hand(ProcessStartInfo started, string command) =>
        PosixShell.Instance.Hand(started, command);
}
