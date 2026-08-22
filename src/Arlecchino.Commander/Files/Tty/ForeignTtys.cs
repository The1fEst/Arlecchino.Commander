namespace Arlecchino.Commander.Files.Tty;

/// <summary>
/// A machine this application has no words for yet. The BSDs make their terminals the POSIX way but by
/// numbers of their own; until each has a dialect here, commands there run on pipes.
/// </summary>
public sealed class ForeignTtys : Ttys
{
    /// <summary>The one of these there is.</summary>
    public static ForeignTtys Instance { get; } = new();

    private ForeignTtys() { }

    /// <inheritdoc/>
    public override bool Works => false;

    /// <inheritdoc/>
    public override Tty? Open(string command, string folder) => null;
}
