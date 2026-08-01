using System;
using System.Collections.Generic;
using System.IO;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Files;

public sealed class LocalSource : IFileSource
{
    public string Label => "local";

    public bool IsRemote => false;

    public int Concurrency => 1;

    public bool TryDeleteTree(FileEntry entry) => false;

    public string Home => Listing.Home();

    /// <summary>
    /// The permissions of a file on this disk. Windows keeps none of the kind a chmod sets, and says
    /// so by refusing the call rather than by inventing an answer.
    /// </summary>
    /// <param name="entry">The file or folder.</param>
    /// <returns>The octal digits, or an empty string on Windows.</returns>
    public string Mode(FileEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (OperatingSystem.IsWindows())
        {
            return "";
        }

        try
        {
            return Modes.Write(Modes.FromUnix(Info(entry).UnixFileMode));
        }
        catch (Exception error) when (IsRefused(error))
        {
            return "";
        }
    }

    public bool TryChangeMode(FileEntry entry, string mode)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (OperatingSystem.IsWindows() || Modes.Read(mode) is not { } wanted)
        {
            return false;
        }

        try
        {
            Info(entry).UnixFileMode = Modes.AsUnix(wanted);

            return true;
        }
        catch (Exception error) when (IsRefused(error))
        {
            return false;
        }
    }

    /// <summary>
    /// Makes a link. A symbolic one the framework of the runtime can make itself; a hard one it
    /// cannot, so that goes through the shell, which both platforms have a spelling for.
    /// </summary>
    /// <param name="path">Where the link goes.</param>
    /// <param name="target">What it points at.</param>
    /// <param name="hard">Whether it is a hard link.</param>
    /// <returns><c>false</c> when the disk refused it.</returns>
    public bool TryLink(string path, string target, bool hard)
    {
        try
        {
            if (hard)
            {
                return Linked(path, target);
            }

            if (Directory.Exists(target))
            {
                Directory.CreateSymbolicLink(path, target);
            }
            else
            {
                File.CreateSymbolicLink(path, target);
            }

            return true;
        }
        catch (Exception error) when (IsRefused(error))
        {
            return false;
        }
    }

    public IShellRun Start(string command, string folder) => new LocalRun(command, folder);

    public bool WalksCheaply => true;

    /// <summary>
    /// Whether the two paths sit on one drive, which is what decides between a rename and a copy.
    /// </summary>
    /// <param name="from">Where it is now.</param>
    /// <param name="target">Where it is going.</param>
    /// <returns><c>true</c> when both are on the same root.</returns>
    public bool SameVolume(string from, string target) => string.Equals(
        Path.GetPathRoot(Path.GetFullPath(from)),
        Path.GetPathRoot(Path.GetFullPath(target)),
        StringComparison.OrdinalIgnoreCase);

    public string Combine(string folder, string name) => Path.Combine(folder, name);

    public string? Parent(string folder) => Listing.Parent(folder);

    public string NameOf(string path) => Path.GetFileName(path);

    public bool FolderExists(string folder) => Directory.Exists(folder);

    public string Free(string folder) => Listing.Free(folder);

    public IReadOnlyList<FileEntry> List(string folder, bool showHidden) => Listing.Read(folder, showHidden);

    public Stream OpenRead(string path) => File.OpenRead(path);

    public Stream Create(string path) => File.Create(path);

    public void CreateFolder(string path) => Directory.CreateDirectory(path);

    public void Delete(FileEntry entry)
    {
        if (entry.IsFolder)
        {
            Directory.Delete(entry.Path, true);
            return;
        }

        File.Delete(entry.Path);
    }

    public void Move(string from, string target)
    {
        if (Directory.Exists(from))
        {
            Directory.Move(from, target);
            return;
        }

        File.Move(from, target, true);
    }

    public void Dispose()
    {
    }

    private static FileSystemInfo Info(FileEntry entry) => entry.IsFolder
        ? new DirectoryInfo(entry.Path)
        : new FileInfo(entry.Path);

    private static bool Linked(string path, string target)
    {
        if (Shells.Local.Link(path, target) is not { } command ||
            Shells.Start(command, Path.GetDirectoryName(path) ?? ".") is not { } started)
        {
            return false;
        }

        using (started)
        {
            Shells.Collect(started);

            return started.ExitCode == 0;
        }
    }

    private static bool IsRefused(Exception error) => error is IOException or UnauthorizedAccessException
        or PlatformNotSupportedException or ArgumentException;
}
