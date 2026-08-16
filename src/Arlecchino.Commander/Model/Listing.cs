using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;

namespace Arlecchino.Commander.Model;

public static class Listing
{
    /// <summary>
    /// Reads a folder off whatever it is on, and says what went wrong instead of throwing. A panel that
    /// cannot read a folder shows the reason where the names would have been.
    /// </summary>
    /// <param name="source">Who to ask.</param>
    /// <param name="folder">Which folder.</param>
    /// <param name="hidden">Whether the hidden files are wanted.</param>
    /// <returns>What is in it, or nothing and the reason.</returns>
    public static async Task<(IReadOnlyList<FileEntry> Entries, string Error)> ReadAsync(
        IFileSource source,
        string folder,
        bool hidden)
    {
        try
        {
            return (await source.ListAsync(folder, hidden, CancellationToken.None).ConfigureAwait(false), "");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                                          InvalidOperationException)
        {
            return ([], error.Message);
        }
    }

    /// <summary>
    /// How much room is left, asked after the listing rather than beside it. Two questions at once
    /// would take two sessions of a pool that has a few, and the names are what the frame is waiting on.
    /// </summary>
    /// <param name="source">Who to ask.</param>
    /// <param name="folder">Where the panel is looking.</param>
    /// <returns>What it said, or nothing when it would not say.</returns>
    public static async Task<string> FreeAsync(IFileSource source, string folder)
    {
        try
        {
            return await source.FreeAsync(folder, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    public static IReadOnlyList<FileEntry> Read(string path, bool showHidden)
    {
        var entries = new List<FileEntry>();

        if (Parent(path) is { } parent)
        {
            entries.Add(new("..", parent, true, true, 0, default, false, false, false));
        }

        foreach (var found in new DirectoryInfo(path).EnumerateFileSystemInfos())
        {
            var hidden = Hidden(found);

            if (hidden && !showHidden)
            {
                continue;
            }

            var isFolder = found.Attributes.HasFlag(FileAttributes.Directory);

            entries.Add(new(
                found.Name,
                found.FullName,
                isFolder,
                false,
                found is FileInfo file ? file.Length : 0,
                Written(found),
                hidden,
                found.Attributes.HasFlag(FileAttributes.ReadOnly),
                !isFolder && Runnable(found)));
        }

        return entries;
    }

    public static int Compare(FileEntry first, FileEntry second, Sorting sorting, bool descending)
    {
        if (first.Rank != second.Rank)
        {
            return first.Rank - second.Rank;
        }

        var order = sorting switch
        {
            Sorting.Size => first.Size.CompareTo(second.Size),
            Sorting.Modified => first.Modified.CompareTo(second.Modified),
            Sorting.Kind => Kind(first, second),
            _ => NaturalSort.Compare(first.Name, second.Name),
        };

        if (order == 0)
        {
            order = NaturalSort.Compare(first.Name, second.Name);
        }

        return descending ? -order : order;
    }

    public static string? Parent(string path)
    {
        try
        {
            return new DirectoryInfo(path).Parent?.FullName;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// The home folder. The environment is asked first, because that is what a shell, a container or
    /// a screenshot run sets when it means somewhere else, while Windows answers
    /// <c>GetFolderPath</c> from the account and ignores <c>USERPROFILE</c> entirely.
    /// </summary>
    /// <returns>The folder, or the working directory when nothing names one.</returns>
    public static string Home()
    {
        string[] wanted =
        [
            Environment.GetEnvironmentVariable("USERPROFILE") ?? "",
            Environment.GetEnvironmentVariable("HOME") ?? "",
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ];

        foreach (var home in wanted)
        {
            if (home.Length > 0 && Directory.Exists(home))
            {
                return home;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    public static IReadOnlyList<string> Drives()
    {
        var roots = new List<string>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady)
            {
                roots.Add(drive.RootDirectory.FullName);
            }
        }

        return roots;
    }

    public static string Free(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));

            return root is null ? "" : Sizes.Free(new DriveInfo(root).AvailableFreeSpace);
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    /// <summary>
    /// The order two rows come in by kind, which is the order their tags come in. A row with no tag has
    /// nothing to stand beside, so it goes after everything that has one.
    /// </summary>
    /// <param name="first">One row.</param>
    /// <param name="second">The other.</param>
    /// <returns>Less than nought when the first of them comes first.</returns>
    private static int Kind(FileEntry first, FileEntry second)
    {
        var one = Kinds.Tag(first);
        var other = Kinds.Tag(second);

        return one.Length == 0 || other.Length == 0
            ? other.Length.CompareTo(one.Length)
            : string.CompareOrdinal(one, other);
    }

    /// <summary>
    /// Whether a name is one to keep back. Windows says so in the attributes, while the rest of the world
    /// says so with a leading dot, which is what a shell and every other file manager there go by.
    /// </summary>
    /// <param name="found">What was read off the disk.</param>
    /// <returns><c>true</c> when it is hidden.</returns>
    private static bool Hidden(FileSystemInfo found) => OperatingSystem.IsWindows()
        ? found.Attributes.HasFlag(FileAttributes.Hidden) || found.Attributes.HasFlag(FileAttributes.System)
        : found.Name.StartsWith('.');

    /// <summary>
    /// Whether the disk will run it. The bit comes off the same call that read the name, so asking costs
    /// nothing; Windows keeps no such bit, and the tag there is read off the extension instead.
    /// </summary>
    /// <param name="found">What was read off the disk.</param>
    /// <returns><c>true</c> when any of the three thirds may run it.</returns>
    private static bool Runnable(FileSystemInfo found)
    {
        const UnixFileMode Executable = UnixFileMode.UserExecute |
                                        UnixFileMode.GroupExecute |
                                        UnixFileMode.OtherExecute;

        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            return (found.UnixFileMode & Executable) != 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static DateTime Written(FileSystemInfo found)
    {
        try
        {
            return found.LastWriteTime;
        }
        catch (IOException)
        {
            return default;
        }
    }
}
