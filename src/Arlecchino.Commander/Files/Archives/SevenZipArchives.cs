using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Arlecchino.Commander.Files.Sources;

namespace Arlecchino.Commander.Files.Archives;

/// <summary>
/// Everything the program reads that holds a table of contents of its own: zips, plain tars, its own
/// format and the container formats. It is asked for the table, then for one entry.
/// </summary>
public sealed class SevenZipArchives : ArchiveFormat
{
    /// <summary>The one of these there is.</summary>
    public static readonly SevenZipArchives Instance = new();

    /// <summary>
    /// The names this reads. They are the formats the program opens in one run, less the disk images
    /// whose names are shared with things that are not archives, and the documents that are zips.
    /// </summary>
    private static readonly string[] Endings =
    [
        ".zip", ".jar", ".war", ".nupkg", ".whl", ".appx", ".msix", ".vsix", ".xpi", ".crx", ".tar",
        ".7z", ".rar", ".cab", ".lzh", ".lha", ".arj", ".xar", ".cpio", ".deb", ".rpm",
        ".iso", ".udf", ".wim", ".swm", ".esd", ".msi", ".msp", ".chm", ".dmg", ".squashfs", ".cramfs",
        ".vhd", ".vhdx", ".vmdk", ".vdi", ".qcow", ".qcow2",
    ];

    /// <summary>Whether a name is one of the formats the program is asked for.</summary>
    /// <param name="name">The file name, in lower case.</param>
    /// <returns><c>true</c> when it is one.</returns>
    public static bool Names(string name)
    {
        foreach (var ending in Endings)
        {
            if (name.EndsWith(ending, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public override IReadOnlyList<ArchiveEntry> Read(string archive)
    {
        var (listing, code) = Said(SevenZip.Starting("l", "-slt", archive));

        if (code != 0)
        {
            throw new IOException(Loc(LocString.ArchiveRefused, SevenZip.Program, Path.GetFileName(archive)));
        }

        return SevenZipListing.Read(listing);
    }

    /// <summary>
    /// Opens one entry, with the program writing it onto its output. What comes back is read as it is
    /// written, so the whole entry is never held at once however large it is.
    /// </summary>
    /// <param name="archive">The archive, as a path on this disk.</param>
    /// <param name="entry">The entry, as its path inside the archive.</param>
    /// <returns>The bytes of that entry.</returns>
    public override Stream Open(string archive, string entry)
    {
        var start = SevenZip.Starting("x", "-so", archive, ArchiveTree.Tidied(entry));
        var running = Process.Start(start) ??
                      throw new IOException(Loc(LocString.ArchiveNoProgram, SevenZip.Program));

        return new LeasedStream(running.StandardOutput.BaseStream, new Stopping(running));
    }

    /// <summary>
    /// Runs the program and reads everything it said. The listing of a large archive is a few hundred
    /// kilobytes of text, which is worth holding for as long as it takes to read it into entries.
    /// </summary>
    /// <param name="start">What to run.</param>
    /// <returns>What it wrote, and how it ended.</returns>
    private static (string Listing, int Code) Said(ProcessStartInfo start)
    {
        using var running = Process.Start(start) ??
                            throw new IOException(Loc(LocString.ArchiveNoProgram, SevenZip.Program));

        var listing = running.StandardOutput.ReadToEnd();

        running.StandardError.ReadToEnd();
        running.WaitForExit();

        return (listing, running.ExitCode);
    }

    /// <summary>
    /// A running program stopped when the reading of it stops. One still writing to a pipe that is read
    /// no further would wait on it for as long as the application runs.
    /// </summary>
    private sealed class Stopping : IDisposable
    {
        private readonly Process _running;

        /// <summary>Takes charge of a running program.</summary>
        /// <param name="running">The program.</param>
        public Stopping(Process running) => _running = running;

        /// <inheritdoc/>
        public void Dispose()
        {
            SevenZip.Stop(_running);
            _running.Dispose();
        }
    }
}
