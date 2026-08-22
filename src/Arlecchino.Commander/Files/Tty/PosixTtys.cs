namespace Arlecchino.Commander.Files.Tty;

/// <summary>
/// The POSIX way: a pair opened by name, with a command at the far end in a session of its own. The
/// numbers and the call spelled otherwise are the machine's, so each machine is a class of its own.
/// </summary>
public abstract class PosixTtys : Ttys
{
    /// <inheritdoc/>
    public override bool Works => true;

    /// <summary>Opening something without taking it over as one's own terminal.</summary>
    internal abstract int NotMine { get; }

    /// <summary>Starting a program in a session of its own.</summary>
    internal abstract short OwnSession { get; }

    /// <summary>Asking a terminal how large its window is.</summary>
    internal abstract nuint AsksWindow { get; }

    /// <summary>Telling a terminal how large its window is.</summary>
    internal abstract nuint TellsWindow { get; }

    /// <summary>
    /// Where the flags of the line discipline sit among the modes. How wide they are there differs as
    /// well, but the one flag read here stands among the lowest of them wherever they are, so the width
    /// is not asked.
    /// </summary>
    internal abstract int LineModesAt { get; }

    /// <inheritdoc/>
    public override Tty? Open(string command, string folder) => PosixTty.Open(this, command, folder);

    /// <summary>Asks a terminal about its window, or tells it.</summary>
    /// <param name="handle">The terminal.</param>
    /// <param name="request">Which of the two.</param>
    /// <param name="window">The size, read or written.</param>
    /// <returns>Nought when it worked.</returns>
    internal abstract int Sizing(int handle, nuint request, ref Posix.Window window);
}
