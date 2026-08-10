using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace Arlecchino.Commander.Model;

/// <summary>
/// Whether a program is on this machine, answered the way a shell answers it: by walking <c>PATH</c>
/// and, on Windows, trying each of the extensions <c>PATHEXT</c> names.
///
/// Offering a list of editors that includes three this machine has never had is worse than offering
/// none — every one of them looks like it would work. So the suggestions are the programs that are
/// really there, and the answer for a given name is worked out once and kept. The hints are rebuilt on
/// every key press, and walking the path on each of them would be a folder listing per letter typed.
/// </summary>
public static class Programs
{
    private static readonly ConcurrentDictionary<string, bool> Known = new(StringComparer.Ordinal);

    /// <summary>Which of these are on this machine, in the order they were offered.</summary>
    /// <param name="names">The programs to look for.</param>
    /// <returns>The ones that are there.</returns>
    public static IReadOnlyList<string> Present(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        var found = new List<string>(names.Count);

        foreach (var name in names)
        {
            if (Known.GetOrAdd(name, Look))
            {
                found.Add(name);
            }
        }

        return found;
    }

    /// <summary>Walks the path looking for one program.</summary>
    /// <param name="name">What to look for.</param>
    /// <returns><c>true</c> when something of that name can be run.</returns>
    private static bool Look(string name)
    {
        try
        {
            foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "")
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (In(folder, name))
                {
                    return true;
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }

        return false;
    }

    /// <summary>Whether one folder holds it, under its own name or under a runnable extension.</summary>
    /// <param name="folder">The folder from the path.</param>
    /// <param name="name">What to look for.</param>
    /// <returns><c>true</c> when it is there.</returns>
    private static bool In(string folder, string name)
    {
        if (folder.Length == 0 || folder.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return false;
        }

        if (File.Exists(Path.Combine(folder, name)))
        {
            return true;
        }

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        foreach (var extension in (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
                 .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (File.Exists(Path.Combine(folder, name + extension)))
            {
                return true;
            }
        }

        return false;
    }
}
