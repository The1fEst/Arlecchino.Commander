using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Files.Ssh;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Tests.Support;

/// <summary>
/// A local disk that also claims it can move a whole file itself, which is what an SFTP server is from
/// the point of view of the work. It exists so the pipelined path can be tested at all: the real one is
/// SSH.NET talking to a server, and there is no server in a test run.
///
/// It counts the two calls rather than only doing them, because what is being asserted is which of the
/// two ends was asked to carry the file — the answer is a choice made in <c>FileTasks</c> and nothing
/// about the resulting file would show it was made wrongly.
/// </summary>
public sealed class PipingSource : IFileSource, IMovesWholeFiles
{
    private readonly LocalSource _disk = new();

    /// <summary>How many whole files were written here.</summary>
    public int Sent { get; private set; }

    /// <summary>How many whole files were read from here.</summary>
    public int Fetched { get; private set; }

    /// <inheritdoc/>
    public async Task SendAsync(Stream reading, string target, CancellationToken token)
    {
        Sent++;

        await using var writing = await _disk.CreateAsync(target, token).ConfigureAwait(false);

        await reading.CopyToAsync(writing, token).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task FetchAsync(string source, Stream writing, CancellationToken token)
    {
        Fetched++;

        await using var reading = await _disk.OpenReadAsync(source, token).ConfigureAwait(false);

        await reading.CopyToAsync(writing, token).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public string Label => _disk.Label;

    /// <inheritdoc/>
    public bool IsRemote => true;

    /// <inheritdoc/>
    public int Concurrency => 1;

    /// <inheritdoc/>
    public string Home => _disk.Home;

    /// <inheritdoc/>
    public bool WalksCheaply => _disk.WalksCheaply;

    /// <inheritdoc/>
    public void Dispose() => _disk.Dispose();

    /// <inheritdoc/>
    public Task<bool> TryDeleteTreeAsync(FileEntry entry, CancellationToken token) =>
        _disk.TryDeleteTreeAsync(entry, token);

    /// <inheritdoc/>
    public Task<string> ModeAsync(FileEntry entry, CancellationToken token) => _disk.ModeAsync(entry, token);

    /// <inheritdoc/>
    public Task<bool> TryChangeModeAsync(FileEntry entry, string mode, CancellationToken token) =>
        _disk.TryChangeModeAsync(entry, mode, token);

    /// <inheritdoc/>
    public Task<bool> TryLinkAsync(string path, string target, bool hard, CancellationToken token) =>
        _disk.TryLinkAsync(path, target, hard, token);

    /// <inheritdoc/>
    public bool HasTrash => _disk.HasTrash;

    /// <inheritdoc/>
    public Task<bool> TryTrashAsync(FileEntry entry, CancellationToken token) =>
        _disk.TryTrashAsync(entry, token);

    /// <inheritdoc/>
    public string Quote(string path) => _disk.Quote(path);

    /// <inheritdoc/>
    public IShellRun Start(string command, string folder) => _disk.Start(command, folder);

    /// <inheritdoc/>
    public bool SameVolume(string from, string target) => _disk.SameVolume(from, target);

    /// <inheritdoc/>
    public string Combine(string folder, string name) => _disk.Combine(folder, name);

    /// <inheritdoc/>
    public string? Parent(string folder) => _disk.Parent(folder);

    /// <inheritdoc/>
    public string NameOf(string path) => _disk.NameOf(path);

    /// <inheritdoc/>
    public Task<bool> FolderExistsAsync(string folder, CancellationToken token) =>
        _disk.FolderExistsAsync(folder, token);

    /// <inheritdoc/>
    public Task<string> FreeAsync(string folder, CancellationToken token) => _disk.FreeAsync(folder, token);

    /// <inheritdoc/>
    public Task<IReadOnlyList<FileEntry>> ListAsync(string folder, bool showHidden, CancellationToken token) =>
        _disk.ListAsync(folder, showHidden, token);

    /// <inheritdoc/>
    public Task<Stream> OpenReadAsync(string path, CancellationToken token) => _disk.OpenReadAsync(path, token);

    /// <inheritdoc/>
    public Task<Stream> CreateAsync(string path, CancellationToken token) => _disk.CreateAsync(path, token);

    /// <inheritdoc/>
    public Task CreateFolderAsync(string path, CancellationToken token) => _disk.CreateFolderAsync(path, token);

    /// <inheritdoc/>
    public Task DeleteAsync(FileEntry entry, CancellationToken token) => _disk.DeleteAsync(entry, token);

    /// <inheritdoc/>
    public Task MoveAsync(string from, string target, CancellationToken token) => _disk.MoveAsync(from, target, token);
}
