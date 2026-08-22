using System;
using System.Diagnostics;

namespace Arlecchino.Commander.Tests.Carries;

/// <summary>
/// Starting a program and hearing what it says, which every terminal here is opened and asked through.
/// Nothing goes through a shell: each word is its own argument and needs no quoting.
/// </summary>
internal static class Processes
{
    /// <summary>How long a program is given to end once it has been told to, in milliseconds.</summary>
    private const int TimeoutMilliseconds = 5000;

    /// <summary>Puts a word in quotes the shell will take off again, whatever it holds.</summary>
    /// <param name="word">The word.</param>
    /// <returns>The quoted word, for a command line a shell will read.</returns>
    internal static string Quoted(string word) => $"'{word.Replace("'", "'\\''", StringComparison.Ordinal)}'";

    /// <summary>Runs a program and waits for the one line it has to say.</summary>
    /// <param name="program">What to run.</param>
    /// <param name="words">What to tell it, one word to an argument.</param>
    /// <returns>What it said, trimmed, and nothing at all when it would not run.</returns>
    internal static string Answered(string program, params string[] words)
    {
        try
        {
            using var running = Process.Start(Telling(program, words, listening: true));

            if (running is null)
            {
                return "";
            }

            var output = running.StandardOutput.ReadToEnd();

            running.WaitForExit();

            return output.Trim();
        }
        catch (Exception failure) when (failure is SystemException or InvalidOperationException)
        {
            return "";
        }
    }

    /// <summary>Starts a program and leaves it running.</summary>
    /// <param name="program">What to run.</param>
    /// <param name="words">What to tell it, one word to an argument.</param>
    /// <returns>The program, or <c>null</c> when it would not start.</returns>
    internal static Process? Started(string program, params string[] words)
    {
        try
        {
            return Process.Start(Telling(program, words, listening: false));
        }
        catch (Exception failure) when (failure is SystemException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Ends a program that is still running, which a terminal is when the try inside it never finished.
    /// Left alone, it holds what the next try is opened under and takes that one down with it.
    /// </summary>
    /// <param name="running">The program, when one was started at all.</param>
    internal static void Ended(Process? running)
    {
        try
        {
            if (running is { HasExited: false })
            {
                running.Kill(entireProcessTree: true);
                _ = running.WaitForExit(TimeoutMilliseconds);
            }
        }
        catch (Exception failure) when (failure is SystemException or InvalidOperationException) { }
    }

    /// <summary>What to start and what to tell it.</summary>
    /// <param name="program">What to run.</param>
    /// <param name="words">What to tell it.</param>
    /// <param name="listening">Whether what it says is to be read back.</param>
    /// <returns>The start.</returns>
    private static ProcessStartInfo Telling(string program, string[] words, bool listening)
    {
        var started = new ProcessStartInfo(program)
        {
            UseShellExecute = false,
            RedirectStandardOutput = listening,
            RedirectStandardError = listening,
        };

        foreach (var word in words)
        {
            started.ArgumentList.Add(word);
        }

        return started;
    }
}
