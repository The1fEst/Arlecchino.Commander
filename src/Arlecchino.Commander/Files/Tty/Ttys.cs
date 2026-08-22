using System;

namespace Arlecchino.Commander.Files.Tty;

/// <summary>
/// One way of making a terminal of one's own: what the machine is asked, and in what words. A machine
/// with no way of its own answers that it has none, and the command goes on pipes instead.
/// </summary>
public abstract class Ttys
{
    /// <summary>
    /// The way this machine makes one. It is decided the once, here and nowhere else, so that a dialect
    /// is added by writing one rather than by finding every place that asks what machine this is.
    /// </summary>
    public static Ttys Local { get; } = OperatingSystem.IsLinux()
        ? PosixTtys.Linux
        : ForeignTtys.Instance;

    /// <summary>Whether a terminal can be made here at all.</summary>
    public abstract bool Works { get; }

    /// <summary>Makes one and starts a command at the far end of it.</summary>
    /// <param name="command">What was typed.</param>
    /// <param name="folder">The folder to run it in.</param>
    /// <returns>The terminal, or <c>null</c> when none could be had.</returns>
    public abstract Tty? Open(string command, string folder);
}
