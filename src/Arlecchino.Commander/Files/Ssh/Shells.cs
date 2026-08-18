using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Arlecchino.Commander.Files.Ssh;

/// <summary>
/// Runs what was typed on the command line through the shell of this machine and hands back what it said.
/// Nothing is interactive: a command goes with its input closed, and <see cref="Shell"/> spells it.
/// </summary>
public static class Shells
{
    /// <summary>The shell of the machine this is running on.</summary>
    public static Shell Local { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? WindowsCommandShell.Instance
        : PosixShell.Instance;

    public static Process? Start(string command, string folder)
    {
        var started = new ProcessStartInfo
        {
            WorkingDirectory = Directory.Exists(folder) ? folder : Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        Local.Hand(started, command);

        try
        {
            return Process.Start(started);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a started process to its end without holding a thread while it thinks, since a command that
    /// prints nothing for a minute is a minute of waiting.
    /// </summary>
    /// <param name="running">The process.</param>
    /// <param name="token">Gives up the wait; the process is left to finish on its own.</param>
    /// <returns>Everything it printed, with how it ended as the last line.</returns>
    public static async Task<List<string>> CollectAsync(Process running, CancellationToken token)
    {
        var lines = new List<string>();

        running.StandardInput.Close();

        var output = running.StandardOutput.ReadToEndAsync(token);
        var problem = running.StandardError.ReadToEndAsync(token);

        lines.AddRange(Split(await output.ConfigureAwait(false)));
        lines.AddRange(Split(await problem.ConfigureAwait(false)));

        await running.WaitForExitAsync(token).ConfigureAwait(false);
        lines.Add($"[exit {running.ExitCode}]");

        return lines;
    }

    public static string[] Split(string text) => text.Length == 0
        ? []
        : text.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
}
