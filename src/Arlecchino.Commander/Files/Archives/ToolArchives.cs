using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Files.Archives;

/// <summary>
/// The formats the runtime does not read, handed to <c>bsdtar</c>, which writes any archive it knows
/// back out as a plain tar. What comes back is read by the same walk a tar off the disk is.
/// </summary>
public sealed class ToolArchives : ArchiveFormat
{
    /// <summary>The program asked, which reads every format it was built with.</summary>
    public const string Program = "bsdtar";

    /// <summary>The one of these there is.</summary>
    public static readonly ToolArchives Instance = new();

    /// <summary>The names this reads, the longest first so a double ending is read whole.</summary>
    private static readonly string[] Endings =
    [
        ".tar.xz", ".tar.bz2", ".tar.zst", ".tar.lz", ".tar.lzma",
        ".txz", ".tbz2", ".tbz", ".tzst",
        ".7z", ".rar", ".cab", ".lha", ".lzh", ".iso", ".xar", ".cpio", ".deb", ".rpm",
    ];

    /// <summary>Whether the program is on this machine, without which none of this is offered.</summary>
    public static bool Works { get; } = Programs.Present([Program]).Count > 0;

    /// <summary>Whether a name is one of the formats handed over.</summary>
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
    /// Everything the archive holds. A program that would not read the file writes nothing and ends
    /// complaining, which is told from an archive that is empty by asking how it ended.
    /// </summary>
    /// <param name="archive">The archive, as a path on this disk.</param>
    /// <returns>The entries, in the order the program wrote them out.</returns>
    public override IReadOnlyList<ArchiveEntry> Read(string archive)
    {
        using var emitting = new Emitting(archive);
        var entries = TarArchives.Listed(emitting.Reader);

        if (entries.Count == 0 && emitting.Refused)
        {
            throw new IOException(Loc(LocString.ArchiveRefused, Program, Path.GetFileName(archive)));
        }

        return entries;
    }

    /// <inheritdoc/>
    public override Stream Open(string archive, string entry)
    {
        var emitting = new Emitting(archive);

        try
        {
            return new LeasedStream(TarArchives.Taken(emitting.Reader, entry, archive), emitting);
        }
        catch
        {
            emitting.Dispose();

            throw;
        }
    }

    /// <summary>
    /// The program writing an archive back out as a tar, and the reader over what it writes. Giving it
    /// up stops the program: one that is still writing when the reading stops would wait on the pipe.
    /// </summary>
    private sealed class Emitting : IDisposable
    {
        private const int Patience = 5000;

        private readonly Process _running;

        /// <summary>Starts the program on one archive.</summary>
        /// <param name="archive">The archive, as a path on this disk.</param>
        public Emitting(string archive)
        {
            var start = new ProcessStartInfo(Program)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                ArgumentList = { "-c", "-f", "-", "@" + archive },
            };

            _running = Process.Start(start) ?? throw new IOException(Loc(LocString.ArchiveNoProgram, Program));

            try
            {
                Reader = new(_running.StandardOutput.BaseStream);
            }
            catch (Exception error)
            {
                Stopped();
                _running.Dispose();

                throw new IOException(error.Message, error);
            }
        }

        /// <summary>The reader over what the program is writing.</summary>
        public TarReader Reader { get; }

        /// <summary>Whether the program would not read the file, which it says by how it ended.</summary>
        public bool Refused
        {
            get
            {
                try
                {
                    return _running.WaitForExit(Patience) && _running.ExitCode != 0;
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
            Reader.Dispose();
            Stopped();

            _running.Dispose();
        }

        /// <summary>
        /// Stops the program if it is still going. One still writing when the reading stopped would
        /// wait on a pipe that is read no further, for as long as the application runs.
        /// </summary>
        private void Stopped()
        {
            try
            {
                if (!_running.HasExited)
                {
                    _running.Kill(entireProcessTree: true);
                }
            }
            catch (Exception error) when (error is InvalidOperationException or Win32Exception or
                                              NotSupportedException) { }
        }
    }
}
