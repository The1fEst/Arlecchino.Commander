using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Work;

namespace Arlecchino.Commander.Files.Archives;

/// <summary>
/// Writing into an archive or out of a whole one, each one run of the program for a whole batch. A
/// copy of ten files is one command where a stream would have been ten.
/// </summary>
public static class SevenZipWriting
{
    private const int Block = 512;

    /// <summary>Adds what is on the disk to an archive.</summary>
    /// <param name="archive">The archive, as a path on this disk.</param>
    /// <param name="folder">The folder the names are under, which the program is run in.</param>
    /// <param name="names">What to add, as names inside that folder.</param>
    /// <param name="outcome">What is being told how it went.</param>
    /// <param name="token">Stops the program, leaving the archive as the program left it.</param>
    /// <returns>A task that finishes when the program has.</returns>
    public static Task AddAsync(
        string archive,
        string folder,
        string[] names,
        Outcome outcome,
        CancellationToken token) =>
        RanAsync(archive, folder, outcome.PlannedBytes, outcome, token, ["a", "-bsp1", "--", archive, .. names]);

    /// <summary>
    /// Unpacks a whole archive into a folder on the disk, in one run of the program. Carrying it out
    /// entry by entry would be a run of the program for every file in it.
    /// </summary>
    /// <param name="archive">The archive, as a path on this disk.</param>
    /// <param name="folder">Where what is in it lands.</param>
    /// <param name="batchBytes">How much of the bar this archive stands for.</param>
    /// <param name="outcome">What is being told how it went.</param>
    /// <param name="token">Stops the program, leaving what it had already written.</param>
    /// <returns>A task that finishes when the program has.</returns>
    public static Task ExtractAsync(
        string archive,
        string folder,
        long batchBytes,
        Outcome outcome,
        CancellationToken token) =>
        RanAsync(archive, "", batchBytes, outcome, token, "x", "-bsp1", "-y", "-o" + folder, "--", archive);

    /// <summary>
    /// Unpacks a tar packed inside another format, as two runs of the program joined by a pipe. One run
    /// alone takes off the outer layer and leaves the tar itself lying in the folder.
    /// </summary>
    /// <param name="archive">The archive, as a path on this disk.</param>
    /// <param name="folder">Where what is in the tar lands.</param>
    /// <param name="batchBytes">How much of the bar this archive stands for.</param>
    /// <param name="outcome">What is being told how it went.</param>
    /// <param name="token">Stops both runs, leaving what was already written.</param>
    /// <returns>A task that finishes when both runs have.</returns>
    public static async Task ExtractPackedAsync(
        string archive,
        string folder,
        long batchBytes,
        Outcome outcome,
        CancellationToken token)
    {
        try
        {
            using var outer = Process.Start(SevenZip.Starting("x", "-so", "-bsp2", "--", archive)) ??
                              throw new IOException(Loc(LocString.ArchiveNoProgram, SevenZip.Program));
            using var inner = Inner(outer, folder);

            var carrying = CarriedAsync(outer, inner);
            var watching = WatchedAsync(outer.StandardError, outcome, batchBytes);
            var droppedOutput = inner.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var complaintText = inner.StandardError.ReadToEndAsync(CancellationToken.None);

            await using (token.Register(() =>
                             {
                                 SevenZip.Stop(outer);
                                 SevenZip.Stop(inner);
                             })
                             .ConfigureAwait(false))
            {
                await Task.WhenAll(
                        outer.WaitForExitAsync(CancellationToken.None),
                        inner.WaitForExitAsync(CancellationToken.None))
                    .ConfigureAwait(false);
            }

            await Task.WhenAll(carrying, watching, droppedOutput).ConfigureAwait(false);

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (inner.ExitCode != 0)
            {
                outcome.Failing(Path.GetFileName(archive), Trimmed(await complaintText.ConfigureAwait(false)));
            }
            else if (outer.ExitCode != 0)
            {
                outcome.Failing(
                    Path.GetFileName(archive),
                    Loc(LocString.ArchiveRefused, SevenZip.Program, Path.GetFileName(archive)));
            }
        }
        catch (Exception error) when (error is IOException or Win32Exception or
                                          InvalidOperationException or PlatformNotSupportedException)
        {
            outcome.Failing(Path.GetFileName(archive), error.Message);
        }
    }

    /// <summary>
    /// Starts the run that reads the tar off the pipe. The outer run is stopped when this one will not
    /// start, since nothing would ever read what it writes.
    /// </summary>
    /// <param name="outer">The run taking off the outer layer.</param>
    /// <param name="folder">Where what is in the tar lands.</param>
    /// <returns>The inner run.</returns>
    private static Process Inner(Process outer, string folder)
    {
        try
        {
            return Process.Start(SevenZip.Starting("x", "-si", "-ttar", "-y", "-o" + folder)) ??
                   throw new IOException(Loc(LocString.ArchiveNoProgram, SevenZip.Program));
        }
        catch
        {
            SevenZip.Stop(outer);

            throw;
        }
    }

    /// <summary>
    /// Carries the tar from one run to the other, closing the pipe at the end, so the inner run knows the
    /// tar is over. An inner run that died leaves the outer one stopped rather than stuck writing.
    /// </summary>
    /// <param name="outer">The run writing the tar.</param>
    /// <param name="inner">The run reading it.</param>
    /// <returns>A task that finishes when the tar is carried or the carrying has broken.</returns>
    private static async Task CarriedAsync(Process outer, Process inner)
    {
        try
        {
            await outer.StandardOutput.BaseStream.CopyToAsync(inner.StandardInput.BaseStream)
                .ConfigureAwait(false);
        }
        catch (IOException)
        {
            SevenZip.Stop(outer);
        }
        finally
        {
            try
            {
                await inner.StandardInput.DisposeAsync().ConfigureAwait(false);
            }
            catch (IOException) { }
        }
    }

    /// <summary>Takes entries out of an archive.</summary>
    /// <param name="archive">The archive, as a path on this disk.</param>
    /// <param name="paths">What to take out, as paths inside the archive.</param>
    /// <param name="outcome">What is being told how it went.</param>
    /// <param name="token">Stops the program.</param>
    /// <returns>A task that finishes when the program has.</returns>
    public static Task RemoveAsync(
        string archive,
        string[] paths,
        Outcome outcome,
        CancellationToken token) =>
        RanAsync(archive, "", outcome.PlannedBytes, outcome, token, ["d", "--", archive, .. paths]);

    /// <summary>Gives an entry of an archive another name.</summary>
    /// <param name="archive">The archive, as a path on this disk.</param>
    /// <param name="path">What it is called now, as a path inside the archive.</param>
    /// <param name="target">What it is to be called.</param>
    /// <param name="outcome">What is being told how it went.</param>
    /// <param name="token">Stops the program.</param>
    /// <returns>A task that finishes when the program has.</returns>
    public static Task RenameAsync(
        string archive,
        string path,
        string target,
        Outcome outcome,
        CancellationToken token) =>
        RanAsync(archive, "", outcome.PlannedBytes, outcome, token, "rn", "--", archive, path, target);

    /// <summary>
    /// Runs the program and waits for it. What it says on its own account is read and dropped; what it
    /// says when it fails is kept, since that is the one thing worth putting on the screen.
    /// </summary>
    /// <param name="archive">The archive, named in the complaint when it fails.</param>
    /// <param name="folder">The folder to run it in, or an empty string for wherever this is.</param>
    /// <param name="batchBytes">How many bytes this run stands for on the bar.</param>
    /// <param name="outcome">What is being told how it went.</param>
    /// <param name="token">Stops the program.</param>
    /// <param name="words">What to run it with.</param>
    /// <returns>A task that finishes when the program has.</returns>
    private static async Task RanAsync(
        string archive,
        string folder,
        long batchBytes,
        Outcome outcome,
        CancellationToken token,
        params string[] words)
    {
        var start = SevenZip.Starting(words);

        if (folder.Length > 0)
        {
            start.WorkingDirectory = folder;
        }

        try
        {
            using var running = Process.Start(start) ??
                                throw new IOException(Loc(LocString.ArchiveNoProgram, SevenZip.Program));

            var watching = WatchedAsync(running.StandardOutput, outcome, batchBytes);
            var complaintText = running.StandardError.ReadToEndAsync(CancellationToken.None);

            await using (token.Register(() => SevenZip.Stop(running)).ConfigureAwait(false))
            {
                await running.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            await watching.ConfigureAwait(false);

            if (running.ExitCode != 0 && !token.IsCancellationRequested)
            {
                outcome.Failing(
                    Path.GetFileName(archive),
                    Trimmed(await complaintText.ConfigureAwait(false)));
            }
        }
        catch (Exception error) when (error is IOException or Win32Exception or
                                          InvalidOperationException or PlatformNotSupportedException)
        {
            outcome.Failing(Path.GetFileName(archive), error.Message);
        }
    }

    /// <summary>
    /// Reads what the program writes while it works, and tells the outcome how far along it is. It
    /// draws over one line rather than writing a line a step, so a step ends where the erasing begins.
    /// </summary>
    /// <param name="reading">What the program is writing.</param>
    /// <param name="outcome">What is being told how far along it is.</param>
    /// <param name="batchBytes">How many bytes this one run of the program stands for.</param>
    /// <returns>A task that finishes when the program has closed its output.</returns>
    private static async Task WatchedAsync(StreamReader reading, Outcome outcome, long batchBytes)
    {
        var buffer = new char[Block];
        var line = new StringBuilder();
        var reachedBytes = 0L;
        var erasing = false;

        while (await reading.ReadAsync(buffer, CancellationToken.None).ConfigureAwait(false) is var read &&
               read > 0)
        {
            for (var at = 0; at < read; at++)
            {
                var letter = buffer[at];

                if (letter is '\r' or '\n')
                {
                    reachedBytes = Stepped(line.ToString(), outcome, batchBytes, reachedBytes);
                    line.Clear();
                    erasing = false;

                    continue;
                }

                if (letter is not '\b')
                {
                    line.Append(letter);
                    erasing = false;

                    continue;
                }

                if (!erasing)
                {
                    reachedBytes = Stepped(line.ToString(), outcome, batchBytes, reachedBytes);
                    erasing = true;
                }

                if (line.Length > 0)
                {
                    line.Length--;
                }
            }
        }

        Stepped(line.ToString(), outcome, batchBytes, reachedBytes);
    }

    /// <summary>
    /// Puts one drawn line on the outcome. The program counts in percentages of its own run and the bar
    /// counts in bytes, so the percentage is turned back into the bytes that run stands for.
    /// </summary>
    /// <param name="line">The line, rebuilt.</param>
    /// <param name="outcome">What is being told how far along it is.</param>
    /// <param name="batchBytes">How many bytes this run of the program stands for.</param>
    /// <param name="reachedBytes">How many bytes the last line accounted for.</param>
    /// <returns>How many are accounted for now.</returns>
    private static long Stepped(string line, Outcome outcome, long batchBytes, long reachedBytes)
    {
        if (SevenZipProgress.Step(line) is not var (percent, name))
        {
            return reachedBytes;
        }

        if (name.Length > 0)
        {
            outcome.Reached(name);
        }

        var wantedBytes = batchBytes * percent / 100;

        if (wantedBytes > reachedBytes)
        {
            outcome.Moved(wantedBytes - reachedBytes);

            return wantedBytes;
        }

        return reachedBytes;
    }

    /// <summary>
    /// What the program complained about, as one line. It writes a banner and a blank line before the
    /// complaint itself, and none of that says anything the screen has room for.
    /// </summary>
    /// <param name="complaint">Everything it wrote on its error stream.</param>
    /// <returns>The complaint, or a word saying it gave none.</returns>
    private static string Trimmed(string complaint)
    {
        foreach (var line in complaint.ReplaceLineEndings("\n").Split('\n'))
        {
            var text = line.Trim();

            if (text.Length > 0 && !text.StartsWith("7-Zip", StringComparison.Ordinal))
            {
                return text;
            }
        }

        return Loc(LocString.ArchiveSaidNothing);
    }
}
