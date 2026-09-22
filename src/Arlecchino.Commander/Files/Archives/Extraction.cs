using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Work;

namespace Arlecchino.Commander.Files.Archives;

/// <summary>
/// Whole archives unpacked into a folder on this disk by 7-Zip. An archive holding one thing at its top
/// lands as that thing, and one holding several lands in a folder named after it.
/// </summary>
public static class Extraction
{
    /// <summary>
    /// Unpacks every archive into the folder. Each is read through once first, so the bar knows how
    /// much all of them come to and each folder is chosen before anything is written.
    /// </summary>
    /// <param name="archives">The archives, as paths on this disk.</param>
    /// <param name="target">The folder they are unpacked into.</param>
    /// <param name="outcome">What is being told how it went.</param>
    /// <param name="token">Stops the unpacking, leaving what was already written.</param>
    /// <returns>A task that finishes when every archive is out.</returns>
    public static async Task UnpackAsync(
        IReadOnlyList<string> archives,
        string target,
        Outcome outcome,
        CancellationToken token)
    {
        var listings = new List<(string Archive, IReadOnlyList<ArchiveEntry> Entries, long Bytes)>();
        var files = 0;
        var folders = 0;
        var bytes = 0L;

        foreach (var archive in archives)
        {
            try
            {
                var entries = Archives.Fallback(archive).Read(archive);
                var size = Sized(entries);

                listings.Add((archive, entries, size.Bytes));
                files += size.Files;
                folders += size.Folders;
                bytes += size.Bytes;
            }
            catch (Exception error) when (error is IOException or InvalidDataException or
                                              UnauthorizedAccessException or NotSupportedException)
            {
                outcome.Failing(Path.GetFileName(archive), error.Message);
            }
        }

        outcome.Planning(new(files, folders, bytes));

        foreach (var (archive, entries, batchBytes) in listings)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            var name = Path.GetFileName(archive);
            var landing = Landing(target, name, Tops(entries));

            outcome.Reached(name);
            Directory.CreateDirectory(landing);

            await (Packed(name)
                    ? SevenZipWriting.ExtractPackedAsync(archive, landing, batchBytes, outcome, token)
                    : SevenZipWriting.ExtractAsync(archive, landing, batchBytes, outcome, token))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Where one archive lands: straight in the folder when it holds a single thing the folder does not
    /// have yet, and otherwise in a folder of its own that nothing is already called.
    /// </summary>
    /// <param name="target">The folder it is unpacked into.</param>
    /// <param name="archive">What the archive is called.</param>
    /// <param name="tops">The names at the top of the archive.</param>
    /// <returns>The folder to unpack into.</returns>
    internal static string Landing(string target, string archive, IReadOnlyList<string> tops)
    {
        if (tops is [var top] && !Taken(Path.Combine(target, top)))
        {
            return target;
        }

        var stem = Stem(archive);
        var folder = Path.Combine(target, stem);

        for (var count = 2; Taken(folder); count++)
        {
            folder = Path.Combine(target, $"{stem} {count}");
        }

        return folder;
    }

    /// <summary>
    /// What an archive is called without its endings. A tar packed in another format has two, and a
    /// folder named after it keeps neither.
    /// </summary>
    /// <param name="archive">What the archive is called.</param>
    /// <returns>The name, never empty.</returns>
    internal static string Stem(string archive)
    {
        var stem = Path.GetFileNameWithoutExtension(archive);

        if (stem.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
        {
            stem = stem[..^4];
        }

        return stem.Length > 0 ? stem : "archive";
    }

    /// <summary>
    /// Whether an archive is a tar packed inside another format, which one run of the program only
    /// takes the outer layer off.
    /// </summary>
    /// <param name="archive">What the archive is called.</param>
    /// <returns><c>true</c> when it takes two runs.</returns>
    internal static bool Packed(string archive)
    {
        var name = archive.ToLowerInvariant();

        return PackedTarArchives.Names(name) ||
               name.EndsWith(".tar.gz", StringComparison.Ordinal) ||
               name.EndsWith(".tgz", StringComparison.Ordinal);
    }

    /// <summary>The names at the top of an archive, each once, however many entries lie under it.</summary>
    /// <param name="entries">What the archive holds.</param>
    /// <returns>The names, in the order the archive first wrote them.</returns>
    internal static List<string> Tops(IReadOnlyList<ArchiveEntry> entries)
    {
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        var tops = new List<string>();

        foreach (var entry in entries)
        {
            var top = ArchiveTree.Tidied(entry.Path).Split('/')[0];

            if (top.Length > 0 && seenNames.Add(top))
            {
                tops.Add(top);
            }
        }

        return tops;
    }

    /// <summary>How much an archive holds, which is what the bar is measured against.</summary>
    /// <param name="entries">What the archive holds.</param>
    /// <returns>The count of files and folders, and the bytes they take once out of the archive.</returns>
    private static Tally Sized(IReadOnlyList<ArchiveEntry> entries)
    {
        var files = 0;
        var folders = 0;
        var bytes = 0L;

        foreach (var entry in entries)
        {
            if (entry.IsFolder)
            {
                folders++;

                continue;
            }

            files++;
            bytes += entry.Size;
        }

        return new(files, folders, bytes);
    }

    /// <summary>Whether something of that name is already there, a file or a folder.</summary>
    /// <param name="path">The path.</param>
    /// <returns><c>true</c> when it is taken.</returns>
    private static bool Taken(string path) => File.Exists(path) || Directory.Exists(path);
}
