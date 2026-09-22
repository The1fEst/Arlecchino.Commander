using System;
using System.Collections.Generic;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Files.Archives;

/// <summary>
/// The flat list an archive holds, read as the folders it stands for. A format lists paths and not much
/// else, and a folder with nothing of its own recorded for it is still a folder the panel walks into.
/// </summary>
public sealed class ArchiveTree
{
    private readonly Dictionary<string, List<FileEntry>> _folders = new(StringComparer.Ordinal);

    /// <summary>Builds the tree out of what a format listed.</summary>
    /// <param name="entries">The entries, in any order.</param>
    public ArchiveTree(IReadOnlyList<ArchiveEntry> entries)
    {
        _folders[RemotePaths.Root] = [];

        foreach (var entry in entries)
        {
            Add(entry);
        }
    }

    /// <summary>Whether there is a folder of that path in the archive.</summary>
    /// <param name="folder">The path, as <c>/src</c>.</param>
    /// <returns><c>true</c> when the panel could be sent there.</returns>
    public bool Has(string folder) => _folders.ContainsKey(folder);

    /// <summary>
    /// What is in one folder, with the way out at the top. At the root the way out names nowhere, since
    /// what is above the root of an archive is the folder the archive itself sits in.
    /// </summary>
    /// <param name="folder">Which folder.</param>
    /// <param name="showHidden">Whether the names beginning with a dot are wanted.</param>
    /// <returns>The rows, in the order they sort in.</returns>
    public IReadOnlyList<FileEntry> List(string folder, bool showHidden)
    {
        if (!_folders.TryGetValue(folder, out var children))
        {
            return [];
        }

        var rows = new List<FileEntry>(children.Count + 1)
        {
            new("..", RemotePaths.Parent(folder) ?? "", true, true, 0, default, false, false, false),
        };

        foreach (var entry in children)
        {
            if (showHidden || !entry.IsHidden)
            {
                rows.Add(entry);
            }
        }

        return rows;
    }

    /// <summary>
    /// Puts one entry in, making every folder above it that the archive did not record. A format is free
    /// to write <c>a/b/c.txt</c> and never mention <c>a</c> or <c>a/b</c>, and most of them do.
    /// </summary>
    /// <param name="entry">The entry.</param>
    private void Add(ArchiveEntry entry)
    {
        var path = Tidied(entry.Path);

        if (path.Length == 0)
        {
            return;
        }

        var folder = RemotePaths.Root;
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        for (var at = 0; at < parts.Length; at++)
        {
            var last = at == parts.Length - 1;
            var isFolder = !last || entry.IsFolder;
            var child = RemotePaths.Combine(folder, parts[at]);

            Record(folder,
                new(parts[at],
                    child,
                    isFolder,
                    false,
                    isFolder ? 0 : entry.Size,
                    last ? entry.Modified : default,
                    RemotePaths.IsHidden(parts[at]),
                    true,
                    false));

            if (!isFolder)
            {
                return;
            }

            _folders.TryAdd(child, []);
            folder = child;
        }
    }

    /// <summary>
    /// Puts a row in a folder, leaving whatever is there under that name. A folder made on the way to
    /// one entry is made again on the way to the next, and the first is kept.
    /// </summary>
    /// <param name="folder">Which folder it goes in.</param>
    /// <param name="entry">The row.</param>
    private void Record(string folder, FileEntry entry)
    {
        var children = _folders[folder];

        foreach (var child in children)
        {
            if (string.Equals(child.Name, entry.Name, StringComparison.Ordinal))
            {
                return;
            }
        }

        children.Add(entry);
    }

    /// <summary>
    /// A path as the tree wants it: no leading slash, no <c>./</c> in front, no trailing slash. Formats
    /// differ on all three, and a path that keeps them makes a folder of its own beside the real one.
    /// </summary>
    /// <param name="path">The path as the archive wrote it.</param>
    /// <returns>The path, or an empty string when nothing is left of it.</returns>
    private static string Tidied(string path)
    {
        var tidiedPath = path.Replace('\\', '/').Trim('/');

        while (tidiedPath.StartsWith("./", StringComparison.Ordinal))
        {
            tidiedPath = tidiedPath[2..].Trim('/');
        }

        return tidiedPath;
    }
}
