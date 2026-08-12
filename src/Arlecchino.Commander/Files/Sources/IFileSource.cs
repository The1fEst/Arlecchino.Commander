using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Files.Ssh;

namespace Arlecchino.Commander.Files.Sources;

/// <summary>
/// Where files come from: a disk, or a server. Everything that waits is asked for rather than done, and what
/// stays synchronous is the shape of a path and what the source knows about itself.
/// </summary>
public interface IFileSource : IDisposable
{
    string Label { get; }

    bool IsRemote { get; }

    /// <summary>
    /// How many requests may be in flight at once. A local disk wants one, and a server wants several, since
    /// it spends the whole time waiting for the network.
    /// </summary>
    int Concurrency { get; }

    string Home { get; }

    /// <summary>
    /// Whether walking a tree costs little enough to do it before the work rather than during it. A local
    /// disk answers a folder at once, where a server spends a round trip on each.
    /// </summary>
    bool WalksCheaply { get; }

    /// <summary>
    /// Whether a deleted file can be put somewhere it could be fetched back from. It is asked before the
    /// work, since it changes what the dialog promises.
    /// </summary>
    bool HasTrash { get; }

    /// <summary>
    /// Puts something where the user could get it back from, instead of deleting it.
    /// </summary>
    /// <param name="entry">The file or folder.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns><c>false</c> when this source has nowhere to put it, leaving it where it was.</returns>
    Task<bool> TryTrashAsync(FileEntry entry, CancellationToken token);

    /// <summary>
    /// Removes a folder and everything under it in one go, when the source can do that itself. A
    /// server reached over SSH can, and one command beats one round trip per file by a long way.
    /// </summary>
    /// <param name="entry">The folder to remove.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns><c>false</c> when the source has no such shortcut and the tree must be walked.</returns>
    Task<bool> TryDeleteTreeAsync(FileEntry entry, CancellationToken token);

    /// <summary>
    /// The permissions of an entry as the octal digits a chmod is written in, <c>755</c> and the like.
    /// </summary>
    /// <param name="entry">The file or folder.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns>The digits, or an empty string on a source that keeps no permissions.</returns>
    Task<string> ModeAsync(FileEntry entry, CancellationToken token);

    /// <summary>Sets the permissions of an entry.</summary>
    /// <param name="entry">The file or folder.</param>
    /// <param name="mode">The octal digits, as typed.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns><c>false</c> when the source keeps no permissions, or refused.</returns>
    Task<bool> TryChangeModeAsync(FileEntry entry, string mode, CancellationToken token);

    /// <summary>Makes a link to something that is already there.</summary>
    /// <param name="path">Where the link goes.</param>
    /// <param name="target">What it points at.</param>
    /// <param name="hard">Whether it is a hard link rather than a symbolic one.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns><c>false</c> when the source cannot make that kind of link.</returns>
    Task<bool> TryLinkAsync(string path, string target, bool hard, CancellationToken token);

    /// <summary>
    /// Wraps a path so that the shell on this end reads it back as one word. Windows and a POSIX shell do not
    /// agree on how, so the end that will read it does the escaping.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The path, ready to stand in a command line.</returns>
    string Quote(string path);

    /// <summary>
    /// Starts a command where the panel is looking, when this source can run one at all.
    /// </summary>
    /// <param name="command">What was typed.</param>
    /// <param name="folder">The folder to run it in.</param>
    /// <returns>The running command, or <c>null</c> when this source runs none.</returns>
    IShellRun? Start(string command, string folder);

    /// <summary>
    /// Whether a move from one path to the other stays on one volume, and so can be a rename rather
    /// than a copy and a deletion.
    /// </summary>
    /// <param name="from">Where it is now.</param>
    /// <param name="target">Where it is going.</param>
    /// <returns><c>true</c> when the move need not copy.</returns>
    bool SameVolume(string from, string target);

    string Combine(string folder, string name);

    string? Parent(string folder);

    string NameOf(string path);

    Task<bool> FolderExistsAsync(string folder, CancellationToken token);

    Task<string> FreeAsync(string folder, CancellationToken token);

    Task<IReadOnlyList<FileEntry>> ListAsync(string folder, bool showHidden, CancellationToken token);

    Task<Stream> OpenReadAsync(string path, CancellationToken token);

    Task<Stream> CreateAsync(string path, CancellationToken token);

    Task CreateFolderAsync(string path, CancellationToken token);

    Task DeleteAsync(FileEntry entry, CancellationToken token);

    Task MoveAsync(string from, string target, CancellationToken token);
}
