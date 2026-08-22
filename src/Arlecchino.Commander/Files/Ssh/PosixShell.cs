using System;
using System.Diagnostics;

namespace Arlecchino.Commander.Files.Ssh;

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
