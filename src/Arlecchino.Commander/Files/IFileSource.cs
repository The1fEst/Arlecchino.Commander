using System;
using System.Collections.Generic;
using System.IO;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Files;

public interface IFileSource : IDisposable
{
    string Label { get; }

    bool IsRemote { get; }

    /// <summary>
    /// How many requests may be in flight at once. A local disk answers immediately and wants one; a
    /// server spends the whole time waiting for the network, where asking for the next thing before
    /// the last one has answered is the difference between a minute and a few seconds.
    /// </summary>
    int Concurrency { get; }

    /// <summary>
    /// Removes a folder and everything under it in one go, when the source can do that itself. A
    /// server reached over SSH can, and one command beats one round trip per file by a long way.
    /// </summary>
    /// <param name="entry">The folder to remove.</param>
    /// <returns><c>false</c> when the source has no such shortcut and the tree must be walked.</returns>
    bool TryDeleteTree(FileEntry entry);

    string Home { get; }

    /// <summary>
    /// The permissions of an entry as the octal digits a chmod is written in, <c>755</c> and the like.
    /// </summary>
    /// <param name="entry">The file or folder.</param>
    /// <returns>The digits, or an empty string on a source that keeps no permissions.</returns>
    string Mode(FileEntry entry);

    /// <summary>Sets the permissions of an entry.</summary>
    /// <param name="entry">The file or folder.</param>
    /// <param name="mode">The octal digits, as typed.</param>
    /// <returns><c>false</c> when the source keeps no permissions, or refused.</returns>
    bool TryChangeMode(FileEntry entry, string mode);

    /// <summary>Makes a link to something that is already there.</summary>
    /// <param name="path">Where the link goes.</param>
    /// <param name="target">What it points at.</param>
    /// <param name="hard">Whether it is a hard link rather than a symbolic one.</param>
    /// <returns><c>false</c> when the source cannot make that kind of link.</returns>
    bool TryLink(string path, string target, bool hard);

    /// <summary>
    /// Starts a command where the panel is looking, when this source can run one at all.
    /// </summary>
    /// <param name="command">What was typed.</param>
    /// <param name="folder">The folder to run it in.</param>
    /// <returns>The running command, or <c>null</c> when this source runs none.</returns>
    IShellRun? Start(string command, string folder);

    /// <summary>
    /// Whether walking a tree costs little enough to do it before the work rather than during it.
    /// A local disk answers a folder at once; a server spends a round trip on each one, which is the
    /// difference between a progress bar and a wait for one.
    /// </summary>
    bool WalksCheaply { get; }

    /// <summary>
    /// Whether a move from one path to the other stays on one volume, and so can be a rename rather
    /// than a copy and a delete.
    /// </summary>
    /// <param name="from">Where it is now.</param>
    /// <param name="target">Where it is going.</param>
    /// <returns><c>true</c> when the move need not copy.</returns>
    bool SameVolume(string from, string target);

    string Combine(string folder, string name);

    string? Parent(string folder);

    string NameOf(string path);

    bool FolderExists(string folder);

    string Free(string folder);

    IReadOnlyList<FileEntry> List(string folder, bool showHidden);

    Stream OpenRead(string path);

    Stream Create(string path);

    void CreateFolder(string path);

    void Delete(FileEntry entry);

    void Move(string from, string target);
}
