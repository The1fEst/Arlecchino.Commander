namespace Arlecchino.Commander.Files.Tty;

/// <summary>
/// The Windows way: the machine makes a console of its own and hands back two pipes, and a command
/// started with that console for its streams believes it is at a terminal — because it is at one.
/// </summary>
public sealed class WindowsTtys : Ttys
{
    /// <summary>The one of these there is.</summary>
    public static WindowsTtys Instance { get; } = new();

    private WindowsTtys() { }

    /// <inheritdoc/>
    public override bool Works => true;

    /// <inheritdoc/>
    public override Tty? Open(string command, string folder) => WindowsTty.Open(command, folder);
}
