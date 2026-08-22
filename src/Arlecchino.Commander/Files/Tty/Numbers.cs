namespace Arlecchino.Commander.Files.Tty;

/// <summary>
/// The numbers a POSIX machine takes for its own. The calls are the same everywhere; what they are handed
/// is not, so each kind of machine brings a table of its own rather than a question at every call.
/// </summary>
/// <param name="NotMine">Opening something without taking it over as one's own terminal.</param>
/// <param name="OwnSession">Starting a program in a session of its own.</param>
/// <param name="AsksWindow">Asking a terminal how large its window is.</param>
/// <param name="TellsWindow">Telling a terminal how large its window is.</param>
/// <param name="LineModesAt">Where the flags of the line discipline sit among the modes.</param>
public sealed record Numbers(
    int NotMine,
    short OwnSession,
    nuint AsksWindow,
    nuint TellsWindow,
    int LineModesAt)
{
    /// <summary>What Linux takes.</summary>
    public static Numbers Linux { get; } = new(
        NotMine: 0x100,
        OwnSession: 0x80,
        AsksWindow: 0x5413,
        TellsWindow: 0x5414,
        LineModesAt: 12);
}
