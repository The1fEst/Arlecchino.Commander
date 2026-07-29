using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Arlecchino.Commander.Files;

/// <summary>
/// Runs what was typed on the command line through the shell of the machine this is running on, and
/// hands back what it said. Nothing is interactive here: a command that asks a question gets no
/// answer, so it is sent with its input closed and whatever it printed is collected.
/// </summary>
public static class Shells
{
    public static Process? Start(string command, string folder)
    {
        var windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var started = new ProcessStartInfo
        {
            FileName = windows ? "cmd.exe" : Interpreter(),
            WorkingDirectory = Directory.Exists(folder) ? folder : Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        started.ArgumentList.Add(windows ? "/c" : "-c");
        started.ArgumentList.Add(command);

        try
        {
            return Process.Start(started);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Reads a started process to its end.</summary>
    /// <param name="running">The process.</param>
    /// <returns>Everything it printed, with how it ended as the last line.</returns>
    public static List<string> Collect(Process running)
    {
        ArgumentNullException.ThrowIfNull(running);

        var lines = new List<string>();

        running.StandardInput.Close();

        lines.AddRange(Split(running.StandardOutput.ReadToEnd()));
        lines.AddRange(Split(running.StandardError.ReadToEnd()));

        running.WaitForExit();
        lines.Add($"[exit {running.ExitCode}]");

        return lines;
    }

    public static string[] Split(string text) => text.Length == 0
        ? []
        : text.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');

    private static string Interpreter() =>
        Environment.GetEnvironmentVariable("SHELL") is { Length: > 0 } shell ? shell : "/bin/sh";
}
