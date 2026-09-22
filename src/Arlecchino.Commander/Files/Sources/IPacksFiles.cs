using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Work;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Files.Sources;

/// <summary>
/// A source written a whole batch at a time rather than a file at a time. An archive is rewritten by
/// the program that owns its format, so one command carries a copy where a stream would carry one file.
/// </summary>
public interface IPacksFiles
{
    /// <summary>Whether anything can be written here at all, which the work asks before it starts.</summary>
    bool Packs { get; }

    /// <summary>Puts files from somewhere else in, under one folder of this source.</summary>
    /// <param name="from">Where the entries live now.</param>
    /// <param name="sources">What to put in.</param>
    /// <param name="folder">Where they go, as a path in this source.</param>
    /// <param name="outcome">What is being told how it went.</param>
    /// <param name="token">Gives up the work.</param>
    /// <returns>A task that finishes when the batch has been written.</returns>
    Task PackAsync(
        IFileSource from,
        IReadOnlyList<FileEntry> sources,
        string folder,
        Outcome outcome,
        CancellationToken token);

    /// <summary>Takes entries out.</summary>
    /// <param name="entries">What to take out.</param>
    /// <param name="outcome">What is being told how it went.</param>
    /// <param name="token">Gives up the work.</param>
    /// <returns>A task that finishes when the batch has been written.</returns>
    Task RemoveAsync(IReadOnlyList<FileEntry> entries, Outcome outcome, CancellationToken token);

    /// <summary>Gives an entry another name, in the folder it is already in.</summary>
    /// <param name="entry">What it is now.</param>
    /// <param name="target">The path it is to have.</param>
    /// <param name="outcome">What is being told how it went.</param>
    /// <param name="token">Gives up the work.</param>
    /// <returns>A task that finishes when the batch has been written.</returns>
    Task RenameAsync(FileEntry entry, string target, Outcome outcome, CancellationToken token);
}
