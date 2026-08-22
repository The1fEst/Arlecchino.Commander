using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Arlecchino.Commander.Files.Ssh;

/// <summary>
/// Runs what was typed on the command line through the shell of this machine, handing back what it
/// says a line at a time. The input is left open, so a question it stops on is answered.
/// </summary>
public static class Shells
{
    /// <summary>How much is taken off a stream at once.</summary>
    private const int Mouthful = 4096;

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
    /// Reads a started process to its end without holding a thread while it thinks, handing every line over
    /// as it arrives rather than at the end. A command that prints for a minute is read for that minute.
    /// </summary>
    /// <param name="running">The process.</param>
    /// <param name="talk">Where what it prints goes, and where a question it stops on goes.</param>
    /// <param name="token">Gives up the wait; the process is left to finish on its own.</param>
    public static async Task TalkAsync(Process running, ShellTalk talk, CancellationToken token)
    {
        var output = ReadAsync(running.StandardOutput, talk, token);
        var problem = ReadAsync(running.StandardError, talk, token);

        await output.ConfigureAwait(false);
        await problem.ConfigureAwait(false);

        await running.WaitForExitAsync(token).ConfigureAwait(false);

        talk.Prints($"[exit {running.ExitCode}]");
    }

    /// <summary>
    /// Reads a process to its end with nothing to say to it. The application's own commands go this way —
    /// there is no one at the keyboard behind them, so their input is closed rather than left waiting.
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

    /// <summary>
    /// Reads one of the two streams a command prints on, a line at a time. What is left over when the
    /// stream goes quiet is a line the command has not finished, which is how a question is spotted.
    /// </summary>
    /// <param name="reading">The stream.</param>
    /// <param name="talk">Where the lines and the questions go.</param>
    /// <param name="token">Gives up the read.</param>
    public static async Task ReadAsync(TextReader reading, ShellTalk talk, CancellationToken token)
    {
        var mouthful = new char[Mouthful];
        var pending = new StringBuilder();

        int characters;

        while ((characters = await reading.ReadAsync(mouthful.AsMemory(), token).ConfigureAwait(false)) > 0)
        {
            for (var at = 0; at < characters; at++)
            {
                var character = mouthful[at];

                if (character == '\r')
                {
                    continue;
                }

                if (character != '\n')
                {
                    pending.Append(character);

                    continue;
                }

                talk.Prints(pending.ToString());
                pending.Clear();
            }

            if (pending.Length == 0 || !Prompts.Asks(pending.ToString(), out var prompt))
            {
                continue;
            }

            talk.Prints(prompt);
            talk.Asks(prompt);

            pending.Clear();
        }

        if (pending.Length > 0)
        {
            talk.Prints(pending.ToString());
        }
    }

    /// <summary>
    /// Sends a line to a command that is running, as typing it at a terminal would. Whether it is read is
    /// the command's own affair: one that is not listening is written to all the same and notices nothing.
    /// </summary>
    /// <param name="running">The process.</param>
    /// <param name="line">What to send, without the newline that ends it.</param>
    /// <returns><c>true</c> when it went.</returns>
    public static bool Say(Process running, string line)
    {
        try
        {
            running.StandardInput.WriteLine(line);
            running.StandardInput.Flush();

            return true;
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Tells a command there is no more input, which is what <c>Ctrl+D</c> does at a terminal. One waiting
    /// on its input for something that is never coming ends where it would otherwise wait for good.
    /// </summary>
    /// <param name="running">The process.</param>
    /// <returns><c>true</c> when the input was open to be closed.</returns>
    public static bool EndInput(Process running)
    {
        try
        {
            running.StandardInput.Close();

            return true;
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException)
        {
            return false;
        }
    }
}
