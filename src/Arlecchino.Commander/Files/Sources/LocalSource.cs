using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Files.Ssh;
using Arlecchino.Commander.Files.Tty;
using Arlecchino.Commander.Files.Watching;
using Arlecchino.Commander.Files.Work;

namespace Arlecchino.Commander.Files.Sources;

/// <summary>
/// The disk this machine has. Reading and writing bytes is asked for rather than done, and the rest is one
/// call into the kernel with no waiting form to ask for.
/// </summary>
public sealed class LocalSource : IFileSource, IWatchesFolder
{
    private const int Block = 128 * 1024;

    public string Label => Loc(LocString.HeaderLocal);

    public bool IsRemote => false;

    public int Concurrency => 1;

    public string Home => Listing.Home();

    public bool WalksCheaply => true;

    public Task<bool> TryDeleteTreeAsync(FileEntry entry, CancellationToken token) => Task.FromResult(false);

    /// <summary>
    /// Asks the operating system to say when the folder changes, which it does whoever changed it — another
    /// program, a shell in another window, or this one.
    /// </summary>
    /// <param name="folder">The folder to watch.</param>
    /// <param name="changed">Called whenever something in it changed.</param>
    /// <returns>What stops the watching, or <c>null</c> when this folder cannot be watched.</returns>
    public IDisposable? Watch(string folder, Action changed) => LocalWatch.Over(folder, changed);

    /// <summary>
    /// The permissions of a file on this disk. Windows keeps none of the kind a chmod sets, and says
    /// so by refusing the call rather than by inventing an answer.
    /// </summary>
    /// <param name="entry">The file or folder.</param>
    /// <param name="token">Unused: the answer is already in hand.</param>
    /// <returns>The octal digits, or an empty string on Windows.</returns>
    public Task<string> ModeAsync(FileEntry entry, CancellationToken token)
    {
        if (OperatingSystem.IsWindows())
        {
            return Task.FromResult("");
        }

        try
        {
            return Task.FromResult(Modes.Write(Modes.FromUnix(Info(entry).UnixFileMode)));
        }
        catch (Exception error) when (IsRefused(error))
        {
            return Task.FromResult("");
        }
    }

    public Task<bool> TryChangeModeAsync(FileEntry entry, string mode, CancellationToken token)
    {
        if (OperatingSystem.IsWindows() || Modes.Read(mode) is not { } wanted)
        {
            return Task.FromResult(false);
        }

        try
        {
            Info(entry).UnixFileMode = Modes.AsUnix(wanted);

            return Task.FromResult(true);
        }
        catch (Exception error) when (IsRefused(error))
        {
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Makes a link. A symbolic one the runtime can make itself; a hard one it cannot, so that goes
    /// through the shell, which both platforms have a spelling for and which is waited on properly.
    /// </summary>
    /// <param name="path">Where the link goes.</param>
    /// <param name="target">What it points at.</param>
    /// <param name="hard">Whether it is a hard link.</param>
    /// <param name="token">Gives up waiting for the shell.</param>
    /// <returns><c>false</c> when the disk refused it.</returns>
    public async Task<bool> TryLinkAsync(string path, string target, bool hard, CancellationToken token)
    {
        try
        {
            if (hard)
            {
                return await LinkedAsync(path, target, token).ConfigureAwait(false);
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

    /// <inheritdoc/>
    public bool HasTrash => Trash.Trash.Current.Works;

    /// <summary>
    /// Puts something in the trash of this machine. It runs off the drawing thread because a folder of
    /// any size is a rename on a good day and a copy on a bad one, and neither belongs in a frame.
    /// </summary>
    /// <param name="entry">The file or folder.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns><c>false</c> when it could not be put there and is still where it was.</returns>
    public Task<bool> TryTrashAsync(FileEntry entry, CancellationToken token)
    {
        return Task.Run(() => Trash.Trash.Current.TryPut(entry.Path), token);
    }

    /// <inheritdoc/>
    public string Quote(string path) => Shells.Local.Quote(path);

    /// <summary>
    /// Starts a command at a terminal of the application's own making, which is the only way a command
    /// that wants the whole screen can ask for it. A machine with no such terminal runs it on pipes.
    /// </summary>
    /// <param name="command">What was typed.</param>
    /// <param name="folder">The folder to run it in.</param>
    /// <returns>The run.</returns>
    public IShellRun Start(string command, string folder) => TerminalRun.Works
        ? new TerminalRun(command, folder)
        : new LocalRun(command, folder);

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

    public Task<bool> FolderExistsAsync(string folder, CancellationToken token) =>
        Task.FromResult(Directory.Exists(folder));

    public Task<string> FreeAsync(string folder, CancellationToken token) => Task.FromResult(Listing.Free(folder));

    public Task<IReadOnlyList<FileEntry>> ListAsync(string folder, bool showHidden, CancellationToken token) =>
        Task.FromResult(Listing.Read(folder, showHidden));

    /// <summary>
    /// Opens a file for reading, asking the operating system for the handle a waiting read needs. On an
    /// ordinary handle every <c>ReadAsync</c> blocks a thread and finishes on the spot.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="token">Unused: opening does not wait.</param>
    /// <returns>The bytes.</returns>
    public Task<Stream> OpenReadAsync(string path, CancellationToken token) =>
        Task.FromResult<Stream>(new FileStream(path,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                BufferSize = Block,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            }));

    public Task<Stream> CreateAsync(string path, CancellationToken token) =>
        Task.FromResult<Stream>(new FileStream(path,
            new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = Block,
                Options = FileOptions.Asynchronous,
            }));

    public Task CreateFolderAsync(string path, CancellationToken token)
    {
        Directory.CreateDirectory(path);

        return Task.CompletedTask;
    }

    public Task DeleteAsync(FileEntry entry, CancellationToken token)
    {
        if (entry.IsFolder)
        {
            Directory.Delete(entry.Path, true);

            return Task.CompletedTask;
        }

        File.Delete(entry.Path);

        return Task.CompletedTask;
    }

    public Task MoveAsync(string from, string target, CancellationToken token)
    {
        if (Directory.Exists(from))
        {
            Directory.Move(from, target);

            return Task.CompletedTask;
        }

        File.Move(from, target, true);

        return Task.CompletedTask;
    }

    public void Dispose() { }

    private static FileSystemInfo Info(FileEntry entry) => entry.IsFolder
        ? new DirectoryInfo(entry.Path)
        : new FileInfo(entry.Path);

    private static async Task<bool> LinkedAsync(string path, string target, CancellationToken token)
    {
        if (Shells.Local.Link(path, target) is not { } command ||
            Shells.Start(command, Path.GetDirectoryName(path) ?? ".") is not { } started)
        {
            return false;
        }

        using (started)
        {
            await Shells.CollectAsync(started, token).ConfigureAwait(false);

            return started.ExitCode == 0;
        }
    }

    private static bool IsRefused(Exception error) => error is IOException or UnauthorizedAccessException
        or PlatformNotSupportedException or ArgumentException;
}
