using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;

namespace Arlecchino.Commander.Files.Archives;

/// <summary>
/// A tar packed inside another format. One run of the program takes the outer layer off onto a pipe,
/// and a second reads the tar off that pipe, since a single run would only ever see the one file.
/// </summary>
public sealed class PackedTarArchives : ArchiveFormat
{
    /// <summary>The one of these there is.</summary>
    public static readonly PackedTarArchives Instance = new();

    /// <summary>The names this reads, the longest first so a double ending is read whole.</summary>
    private static readonly string[] Endings =
    [
        ".tar.gz", ".tar.xz", ".tar.bz2", ".tar.zst", ".tar.lz", ".tar.lzma", ".tar.z",
        ".tgz", ".txz", ".tbz2", ".tbz", ".tzst",
    ];

    /// <summary>Whether a name is a packed tar, which is two formats one inside the other.</summary>
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

    /// <summary>
    /// Everything the archive holds. An outer layer the program would not take off leaves the tar
    /// empty, which is told from an archive that is empty by asking how the first run ended.
    /// </summary>
    /// <param name="archive">The archive, as a path on this disk.</param>
    /// <returns>The entries, in the order the program wrote them out.</returns>
    public override IReadOnlyList<ArchiveEntry> Read(string archive)
    {
        using var piping = new Piping(archive, "l", "-slt", "-si", "-ttar");
        var listing = piping.Inner.StandardOutput.ReadToEnd();

        piping.Inner.WaitForExit();

        if (piping.Refused || piping.Inner.ExitCode != 0)
        {
            throw new IOException(Loc(LocString.ArchiveRefused, SevenZip.Program, Path.GetFileName(archive)));
        }

        return SevenZipListing.Read(listing);
    }

    /// <summary>
    /// Opens one entry, with the second run writing it onto its output. Both runs are stopped when the
    /// reading stops, which is how a file only partly read gives the pipes back.
    /// </summary>
    /// <param name="archive">The archive, as a path on this disk.</param>
    /// <param name="entry">The entry, as its path inside the archive.</param>
    /// <returns>The bytes of that entry.</returns>
    public override Stream Open(string archive, string entry)
    {
        var piping = new Piping(archive, "x", "-so", "-si", "-ttar", "--", ArchiveTree.Tidied(entry));

        return new LeasedStream(piping.Inner.StandardOutput.BaseStream, piping);
    }

    /// <summary>
    /// Two runs of the program joined by a pipe: the first takes the outer layer off the archive, the
    /// second reads the tar that comes out. Giving it up stops both.
    /// </summary>
    private sealed class Piping : IDisposable
    {
        private const int Patience = 5000;

        private readonly Process _outer;
        private readonly Task _carrying;

        /// <summary>Starts both runs on one archive.</summary>
        /// <param name="archive">The archive, as a path on this disk.</param>
        /// <param name="words">What to run the second with, which reads the tar off its input.</param>
        public Piping(string archive, params string[] words)
        {
            _outer = Process.Start(SevenZip.Starting("x", "-so", "--", archive)) ??
                     throw new IOException(Loc(LocString.ArchiveNoProgram, SevenZip.Program));

            try
            {
                Inner = Process.Start(SevenZip.Starting(words)) ??
                        throw new IOException(Loc(LocString.ArchiveNoProgram, SevenZip.Program));
            }
            catch
            {
                SevenZip.Stop(_outer);
                _outer.Dispose();

                throw;
            }

            _ = _outer.StandardError.ReadToEndAsync();
            _ = Inner.StandardError.ReadToEndAsync();
            _carrying = Task.Run(Carried);
        }

        /// <summary>The run reading the tar, whose output is what was asked for.</summary>
        public Process Inner { get; }

        /// <summary>Whether the first run would not take the outer layer off, which it says by how it ended.</summary>
        public bool Refused
        {
            get
            {
                try
                {
                    return _outer.WaitForExit(Patience) && _outer.ExitCode != 0;
                }
                catch (Exception error) when (error is InvalidOperationException or SystemException)
                {
                    return true;
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            SevenZip.Stop(Inner);
            SevenZip.Stop(_outer);

            try
            {
                _carrying.Wait(Patience);
            }
            catch (AggregateException) { }

            Inner.Dispose();
            _outer.Dispose();
        }

        /// <summary>
        /// Carries the tar across, closing the pipe at the end, so the second run knows the tar is over.
        /// A second run that died leaves the first stopped rather than stuck writing.
        /// </summary>
        private void Carried()
        {
            try
            {
                _outer.StandardOutput.BaseStream.CopyTo(Inner.StandardInput.BaseStream);
            }
            catch (Exception error) when (error is IOException or ObjectDisposedException)
            {
                SevenZip.Stop(_outer);
            }
            finally
            {
                try
                {
                    Inner.StandardInput.Dispose();
                }
                catch (Exception error) when (error is IOException or ObjectDisposedException) { }
            }
        }
    }
}
