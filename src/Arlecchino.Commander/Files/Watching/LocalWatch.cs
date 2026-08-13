using System;
using System.IO;

namespace Arlecchino.Commander.Files.Watching;

/// <summary>
/// One folder on this disk, watched by the operating system. Every kind of event is the same news to a
/// panel — the folder is not what it was — so all of them are forwarded as one.
/// </summary>
internal sealed class LocalWatch : IDisposable
{
    private readonly FileSystemWatcher _watcher;

    private LocalWatch(FileSystemWatcher watcher) => _watcher = watcher;

    /// <summary>Starts watching a folder, and nothing under it.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="changed">Called on another thread whenever something changed.</param>
    /// <returns>The watch, or <c>null</c> when this folder cannot be watched at all.</returns>
    public static LocalWatch? Over(string folder, Action changed)
    {
        FileSystemWatcher? watcher = null;

        try
        {
            watcher = new(folder)
            {
                NotifyFilter = NotifyFilters.FileName |
                               NotifyFilters.DirectoryName |
                               NotifyFilters.LastWrite |
                               NotifyFilters.Size |
                               NotifyFilters.Attributes,
                IncludeSubdirectories = false,
            };

            watcher.Created += (_, _) => changed();
            watcher.Deleted += (_, _) => changed();
            watcher.Changed += (_, _) => changed();
            watcher.Renamed += (_, _) => changed();
            watcher.Error += (_, _) => changed();

            watcher.EnableRaisingEvents = true;

            return new(watcher);
        }
        catch (Exception error) when (IsRefused(error))
        {
            watcher?.Dispose();

            return null;
        }
    }

    /// <summary>Stops the watching, which disposing the watcher does by itself.</summary>
    public void Dispose() => _watcher.Dispose();

    /// <summary>
    /// Whether the disk said no rather than broke. A folder that has gone, one the account may not read and
    /// a platform with nothing to watch with all end here, and all of them mean the same: no watch.
    /// </summary>
    /// <param name="error">What was thrown.</param>
    /// <returns><c>true</c> when the folder simply cannot be watched.</returns>
    private static bool IsRefused(Exception error) => error is ArgumentException or IOException or
        UnauthorizedAccessException or PlatformNotSupportedException or ObjectDisposedException;
}
