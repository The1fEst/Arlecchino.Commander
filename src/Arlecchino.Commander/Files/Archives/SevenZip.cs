using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Security;
using Arlecchino.Commander.Model;
using Microsoft.Win32;

namespace Arlecchino.Commander.Files.Archives;

/// <summary>
/// The program every format the runtime cannot read is handed to, and the one thing that can write into
/// an archive. It is the same program on all three platforms, under whichever of its names is installed.
/// </summary>
public static class SevenZip
{
    /// <summary>
    /// The names it is installed under, in the order they are tried. The first is what the project
    /// ships its own build as, and the second what a distribution packages it as.
    /// </summary>
    private static readonly string[] Names = ["7zz", "7z", "7za"];

    /// <summary>
    /// What it is called on this machine, or an empty string when it is not on it. A name on the path
    /// is run by its name, and a copy the installer kept off the path by its whole path.
    /// </summary>
    public static string Program { get; } = Found();

    /// <summary>Whether the program is here at all, without which none of this is offered.</summary>
    public static bool Works => Program.Length > 0;

    /// <summary>
    /// A run of the program, with everything it says on its own account kept off the terminal. Its
    /// progress goes to the standard output and its complaints to the error, and both are read.
    /// </summary>
    /// <param name="words">What to run it with.</param>
    /// <returns>The start, ready to run.</returns>
    public static ProcessStartInfo Starting(params string[] words)
    {
        var start = new ProcessStartInfo(Program)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };

        foreach (var word in words)
        {
            start.ArgumentList.Add(word);
        }

        return start;
    }

    /// <summary>
    /// Stops a run of the program if it is still going. One still writing to a pipe that is no longer
    /// read would wait on it for as long as the application runs.
    /// </summary>
    /// <param name="running">The run.</param>
    public static void Stop(Process running)
    {
        try
        {
            if (!running.HasExited)
            {
                running.Kill(entireProcessTree: true);
            }
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or
                                          NotSupportedException) { }
    }

    private static string Found()
    {
        foreach (var name in Names)
        {
            if (Programs.Present([name]).Count > 0)
            {
                return name;
            }
        }

        return OperatingSystem.IsWindows() ? InstallerPath(InstallFolders()) : "";
    }

    /// <summary>
    /// The folders the Windows installer may have put the program in, off the path. The registry key
    /// the installer writes comes first, and the usual folder for programs after it.
    /// </summary>
    /// <returns>The folders, the ones the registry names first.</returns>
    [SupportedOSPlatform("windows")]
    internal static List<string> InstallFolders()
    {
        var folders = new List<string>();

        foreach (var hive in (RegistryHive[])[RegistryHive.CurrentUser, RegistryHive.LocalMachine])
        {
            foreach (var view in (RegistryView[])[RegistryView.Registry64, RegistryView.Registry32])
            {
                try
                {
                    using var root = RegistryKey.OpenBaseKey(hive, view);
                    using var key = root.OpenSubKey(@"Software\7-Zip");

                    foreach (var name in (string[])["Path64", "Path"])
                    {
                        if (key?.GetValue(name) is string { Length: > 0 } folder)
                        {
                            folders.Add(folder);
                        }
                    }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                                                  SecurityException) { }
            }
        }

        foreach (var special in (Environment.SpecialFolder[])
                 [
                     Environment.SpecialFolder.ProgramFiles,
                     Environment.SpecialFolder.ProgramFilesX86,
                 ])
        {
            if (Environment.GetFolderPath(special) is { Length: > 0 } programs)
            {
                folders.Add(Path.Combine(programs, "7-Zip"));
            }
        }

        return folders;
    }

    /// <summary>The program in the first of these folders that holds it.</summary>
    /// <param name="folders">The folders 7-Zip may be installed in.</param>
    /// <returns>The whole path to the program, or an empty string when it is in none of them.</returns>
    internal static string InstallerPath(IReadOnlyList<string> folders)
    {
        foreach (var folder in folders)
        {
            if (folder.Length == 0)
            {
                continue;
            }

            var path = Path.Combine(folder, "7z.exe");

            if (File.Exists(path))
            {
                return path;
            }
        }

        return "";
    }
}
