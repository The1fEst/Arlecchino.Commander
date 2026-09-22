using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Arlecchino.Commander.Files.Opening;

/// <summary>
/// What hands a file to the program this machine keeps for files of its kind, which every desktop
/// spells differently. A server has no such thing, so this is only ever about a file on this machine.
/// </summary>
public abstract class Opener
{
    /// <summary>The one this machine has.</summary>
    public static Opener Current { get; } = Pick();

    /// <summary>How the file would be started.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The program and its words, or <c>null</c> when this machine opens nothing.</returns>
    public abstract ProcessStartInfo? Starting(string path);

    /// <summary>
    /// Hands the file over and does not wait: what it opens in is a window of its own, and the panels
    /// go on. What the handler says on the way is read and dropped rather than written into a frame.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns><c>false</c> when nothing here opens it, or the handler would not start.</returns>
    public bool TryOpen(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (Starting(path) is not { } start)
        {
            return false;
        }

        try
        {
            if (Process.Start(start) is not { } running)
            {
                return false;
            }

            if (start.RedirectStandardOutput)
            {
                _ = Drained(running);
            }
            else
            {
                running.Dispose();
            }

            return true;
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or
                                          PlatformNotSupportedException or ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads both of the handler's streams to their end and throws away what they said. Unread they
    /// would stop the handler once the pipe filled, and unclosed they would outlast every file opened.
    /// </summary>
    /// <param name="running">The handler.</param>
    /// <returns>A task that finishes when the handler has closed both.</returns>
    private static async Task Drained(Process running)
    {
        using (running)
        {
            await Task.WhenAll(
                    running.StandardOutput.ReadToEndAsync(),
                    running.StandardError.ReadToEndAsync())
                .ConfigureAwait(false);
        }
    }

    private static Opener Pick()
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsOpener.Instance;
        }

        if (OperatingSystem.IsMacOS())
        {
            return MacOpener.Instance;
        }

        return OperatingSystem.IsLinux() ? FreedesktopOpener.Instance : NoOpener.Instance;
    }
}

/// <summary>
/// A machine with nothing to hand a file to. Opening there is refused outright rather than reported as
/// a window that went up somewhere the user cannot see.
/// </summary>
public sealed class NoOpener : Opener
{
    /// <summary>The one of these there is.</summary>
    public static readonly NoOpener Instance = new();

    /// <inheritdoc/>
    public override ProcessStartInfo? Starting(string path) => null;
}
