using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Files.Ssh;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Files.Archives;

/// <summary>
/// An archive browsed as though it were a folder. A panel connects to it exactly as it connects to a
/// server, which is what lets copying out of one be the same key as copying off a disk.
/// </summary>
public sealed class ArchiveSource : IFileSource
{
    private readonly ArchiveFormat _format;
    private readonly ArchiveTree _tree;

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

    /// <inheritdoc/>
    public void Dispose() { }

    private IOException ReadOnly() => new(Loc(LocString.ArchiveReadOnly, Label));
}
