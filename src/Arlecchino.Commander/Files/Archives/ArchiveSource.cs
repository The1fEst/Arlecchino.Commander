using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Files.Ssh;
using Arlecchino.Commander.Files.Work;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Files.Archives;

/// <summary>
/// An archive browsed as though it were a folder. A panel connects to it exactly as it connects to a
/// server, which is what lets copying out of one be the same key as copying off a disk.
/// </summary>
public sealed class ArchiveSource : IFileSource, IPacksFiles
{
    private readonly ArchiveFormat _format;

    private ArchiveTree _tree;

    /// <summary>Opens an archive, reading what is in it once.</summary>
    /// <param name="path">The archive, as a path on this disk.</param>
    /// <param name="format">What reads archives of this kind.</param>
    public ArchiveSource(string path, ArchiveFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Path = path;
        _format = format;
        _tree = new(format.Read(path));
    }

    /// <summary>The archive itself, as a path on this disk.</summary>
    public string Path { get; }

    /// <summary>The folder the archive sits in, which is where leaving it puts the panel back.</summary>
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? "";

    /// <inheritdoc/>
    public string Label => System.IO.Path.GetFileName(Path);

    /// <summary>
    /// Whether the panel is somewhere other than this disk, which an archive is: there is no path in it
    /// to hand to an editor or to the program that opens a file, and leaving it closes it.
    /// </summary>
    public bool IsRemote => true;

    /// <inheritdoc/>
    public int Concurrency => 1;

    /// <inheritdoc/>
    public string Home => RemotePaths.Root;

    /// <inheritdoc/>
    public bool WalksCheaply => true;

    /// <inheritdoc/>
    public bool HasTrash => false;

    /// <inheritdoc/>
    public Task<bool> TryTrashAsync(FileEntry entry, CancellationToken token) => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<bool> TryDeleteTreeAsync(FileEntry entry, CancellationToken token) => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<string> ModeAsync(FileEntry entry, CancellationToken token) => Task.FromResult("");

    /// <inheritdoc/>
    public Task<bool> TryChangeModeAsync(FileEntry entry, string mode, CancellationToken token) =>
        Task.FromResult(false);

    /// <inheritdoc/>
    public Task<bool> TryLinkAsync(string path, string target, bool hard, CancellationToken token) =>
        Task.FromResult(false);

    /// <inheritdoc/>
    public string Quote(string path) => path;

    /// <inheritdoc/>
    public IShellRun? Start(string command, string folder) => null;

    /// <inheritdoc/>
    public bool SameVolume(string from, string target) => false;

    /// <inheritdoc/>
    public string Combine(string folder, string name) => RemotePaths.Combine(folder, name);

    /// <inheritdoc/>
    public string? Parent(string folder) => RemotePaths.Parent(folder);

    /// <inheritdoc/>
    public string NameOf(string path) => RemotePaths.NameOf(path);

    /// <inheritdoc/>
    public Task<bool> FolderExistsAsync(string folder, CancellationToken token) =>
        Task.FromResult(_tree.Has(folder));

    /// <summary>
    /// How much room is left, which an archive has none of to report: it is as large as it is and
    /// nothing can be written into it.
    /// </summary>
    /// <param name="folder">Unused.</param>
    /// <param name="token">Unused.</param>
    /// <returns>An empty string.</returns>
    public Task<string> FreeAsync(string folder, CancellationToken token) => Task.FromResult("");

    /// <inheritdoc/>
    public Task<IReadOnlyList<FileEntry>> ListAsync(string folder, bool showHidden, CancellationToken token) =>
        Task.FromResult(_tree.List(folder, showHidden));

    /// <inheritdoc/>
    public Task<Stream> OpenReadAsync(string path, CancellationToken token) =>
        Task.FromResult(_format.Open(Path, path.TrimStart('/')));

    /// <inheritdoc/>
    public Task<Stream> CreateAsync(string path, CancellationToken token) => throw ReadOnly();

    /// <inheritdoc/>
    public Task CreateFolderAsync(string path, CancellationToken token) => throw ReadOnly();

    /// <inheritdoc/>
    public Task DeleteAsync(FileEntry entry, CancellationToken token) => throw ReadOnly();

    /// <inheritdoc/>
    public Task MoveAsync(string from, string target, CancellationToken token) => throw ReadOnly();

    /// <summary>
    /// Whether anything can be written into this archive. Only the three formats the program rewrites
    /// in place are offered: the two-layer ones and the formats it only reads refuse outright.
    /// </summary>
    public bool Packs => SevenZip.Works && Archives.Writable(Path);

    /// <summary>
    /// Why nothing can be written into this archive. A format the program rewrites is refused only
    /// because the program is not on this machine, which is worth saying rather than blaming the file.
    /// </summary>
    public string Refusal => Archives.Refusal(Path, SevenZip.Works);

    /// <inheritdoc/>
    public async Task PackAsync(
        IFileSource from,
        IReadOnlyList<FileEntry> sources,
        string folder,
        Outcome outcome,
        CancellationToken token)
    {
        var (working, names, staged) = await StagedAsync(from, sources, folder, outcome, token)
            .ConfigureAwait(false);

        try
        {
            if (names.Length > 0 && !token.IsCancellationRequested)
            {
                await SevenZipWriting.AddAsync(Path, working, names, outcome, token).ConfigureAwait(false);
            }
        }
        finally
        {
            Swept(staged);
            Reread();
        }
    }

    /// <summary>
    /// What the program is run on: a folder holding the files, and the names to add out of it. The
    /// program takes files off a disk, so anything not on one is laid out in a folder first.
    /// </summary>
    /// <param name="from">Where the entries live now.</param>
    /// <param name="sources">What is going in.</param>
    /// <param name="folder">Where they go, as a path inside the archive.</param>
    /// <param name="outcome">What is being told how it went.</param>
    /// <param name="token">Gives up the work.</param>
    /// <returns>The folder to run in, the names to add, and the folder to sweep away at the end.</returns>
    private static async Task<(string Working, string[] Names, string Staged)> StagedAsync(
        IFileSource from,
        IReadOnlyList<FileEntry> sources,
        string folder,
        Outcome outcome,
        CancellationToken token)
    {
        var insidePath = ArchiveTree.Tidied(folder);

        if (insidePath.Length == 0 &&
            from is LocalSource &&
            System.IO.Path.GetDirectoryName(sources[0].Path) is { Length: > 0 } beside)
        {
            return (beside, [.. sources.Select(static entry => entry.Name)], "");
        }

        var stagedFolder = Directory.CreateTempSubdirectory("commander-packing").FullName;
        var landing = insidePath.Length == 0
            ? stagedFolder
            : Directory.CreateDirectory(System.IO.Path.Combine(
                    stagedFolder,
                    insidePath.Replace('/', System.IO.Path.DirectorySeparatorChar)))
                .FullName;

        using var disk = new LocalSource();

        await FileTasks.CopyAsync(from, sources, disk, landing, outcome, token).ConfigureAwait(false);

        var topNames = insidePath.Length == 0
            ? sources.Select(static entry => entry.Name).ToArray()
            : [insidePath.Split('/')[0]];

        return (stagedFolder, topNames, stagedFolder);
    }

    /// <summary>Gets rid of what was laid out for the program, however the run went.</summary>
    /// <param name="stagedFolder">The folder, or an empty string when nothing was laid out.</param>
    private static void Swept(string stagedFolder)
    {
        if (stagedFolder.Length == 0)
        {
            return;
        }

        try
        {
            Directory.Delete(stagedFolder, true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(IReadOnlyList<FileEntry> entries, Outcome outcome, CancellationToken token)
    {
        var paths = new string[entries.Count];

        for (var at = 0; at < entries.Count; at++)
        {
            paths[at] = ArchiveTree.Tidied(entries[at].Path);
            outcome.Reached(entries[at].Name);
        }

        await SevenZipWriting.RemoveAsync(Path, paths, outcome, token).ConfigureAwait(false);

        Reread();
    }

    /// <inheritdoc/>
    public async Task RenameAsync(FileEntry entry, string target, Outcome outcome, CancellationToken token)
    {
        outcome.Reached(entry.Name);

        await SevenZipWriting
            .RenameAsync(Path, ArchiveTree.Tidied(entry.Path), ArchiveTree.Tidied(target), outcome, token)
            .ConfigureAwait(false);

        Reread();
    }

    /// <inheritdoc/>
    public void Dispose() { }

    /// <summary>
    /// Reads the table of contents again, the archive having been rewritten under it. The panel asks
    /// this source for the folder again when the work finishes, and would be given the old table.
    /// </summary>
    private void Reread()
    {
        try
        {
            _tree = new(_format.Read(Path));
        }
        catch (Exception error) when (error is IOException or InvalidDataException or
                                          UnauthorizedAccessException or NotSupportedException) { }
    }

    private IOException ReadOnly() => new(Refusal);
}
