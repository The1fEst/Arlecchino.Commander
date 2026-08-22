namespace Arlecchino.Commander.Files.Tty;

/// <summary>
/// A machine this application has no words for yet. Windows makes its terminals through a console of its
/// own and macOS through numbers of its own; until each has a dialect here, commands run on pipes.
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
