using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Arlecchino.Commander.Model;

/// <summary>
/// Whether a program is on this machine, answered the way a shell answers it: by walking <c>PATH</c> and,
/// on Windows, trying each of the extensions <c>PATHEXT</c> names. The answer for a name is kept.
/// </summary>
public static class Programs
{
    private static readonly ConcurrentDictionary<string, bool> Cache = new(StringComparer.Ordinal);

    private static readonly Lazy<IReadOnlyList<string>> All = new(Everything, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Everything runnable whose name begins this way, for finishing a half-typed command. The path is
    /// walked once and the names are kept, since a machine does not grow programs while it is being typed on.
    /// </summary>
    /// <param name="start">What was typed of the name.</param>
    /// <returns>The names, in the order they sort in.</returns>
    public static List<string> Starting(string start)
    {
        var match = new List<string>();

        foreach (var name in All.Value)
        {
            if (name.StartsWith(start, StringComparison.OrdinalIgnoreCase))
            {
                match.Add(name);
            }
        }

        return match;
    }

    /// <summary>Which of these are on this machine, in the order they were offered.</summary>
    /// <param name="names">The programs to look for.</param>
    /// <returns>The ones that are there.</returns>
    public static IReadOnlyList<string> Present(IReadOnlyList<string> names)
    {
        var match = new List<string>(names.Count);

        foreach (var name in names)
        {
            if (Cache.GetOrAdd(name, Look))
            {
                match.Add(name);
            }
        }

        return match;
    }

    /// <summary>
    /// Every name on the path, sorted and without repeats. On Windows a program is known by an extension
    /// from <c>PATHEXT</c>, which is taken off, and everywhere else by the leave to run it.
    /// </summary>
    /// <returns>The names.</returns>
    private static IReadOnlyList<string> Everything()
    {
        var match = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "")
                 .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (folder.Length == 0 || folder.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                continue;
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(folder))
                {
                    if (Runnable(file) is { Length: > 0 } name)
                    {
                        match.Add(name);
                    }
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                                              ArgumentException) { }
        }

        return [.. match];
    }

    /// <summary>What one file on the path is worth offering as, or nothing when it cannot be run.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The name to type, without the extension Windows hides.</returns>
    private static string? Runnable(string file)
    {
        if (!OperatingSystem.IsWindows())
        {
            const UnixFileMode Executable = UnixFileMode.UserExecute |
                                            UnixFileMode.GroupExecute |
                                            UnixFileMode.OtherExecute;

            return (File.GetUnixFileMode(file) & Executable) == 0 ? null : Path.GetFileName(file);
        }

        var extension = Path.GetExtension(file);

        foreach (var runnable in (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
                 .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (extension.Equals(runnable, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFileNameWithoutExtension(file);
            }
        }

        return null;
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
