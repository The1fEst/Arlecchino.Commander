using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Work;

namespace Arlecchino.Commander.Files.Archives;

/// <summary>
/// Writing into an archive, which is one run of the program for a whole batch. The program rewrites the
/// archive itself, so a copy of ten files is one command where a stream would have been ten.
/// </summary>
public static class SevenZipWriting
{
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
        RanAsync(archive, folder, outcome, token, ["a", "--", archive, .. names]);

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
        RanAsync(archive, "", outcome, token, ["d", "--", archive, .. paths]);

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
        RanAsync(archive, "", outcome, token, "rn", "--", archive, path, target);

    /// <summary>
    /// Runs the program and waits for it. What it says on its own account is read and dropped; what it
    /// says when it fails is kept, since that is the one thing worth putting on the screen.
    /// </summary>
    /// <param name="archive">The archive, named in the complaint when it fails.</param>
    /// <param name="folder">The folder to run it in, or an empty string for wherever this is.</param>
    /// <param name="outcome">What is being told how it went.</param>
    /// <param name="token">Stops the program.</param>
    /// <param name="words">What to run it with.</param>
    /// <returns>A task that finishes when the program has.</returns>
    private static async Task RanAsync(
        string archive,
        string folder,
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

            var saidText = running.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var complaintText = running.StandardError.ReadToEndAsync(CancellationToken.None);

            await using (token.Register(() => Stopped(running)).ConfigureAwait(false))
            {
                await running.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            await saidText.ConfigureAwait(false);

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

    private static void Stopped(Process running)
    {
        try
        {
            if (!running.HasExited)
            {
                running.Kill(entireProcessTree: true);
            }
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or
                                          NotSupportedException) { }
    }
}
