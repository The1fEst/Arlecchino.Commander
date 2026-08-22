namespace Arlecchino.Commander.Files.Tty;

/// <summary>
/// The POSIX way: a pair opened by name, a command started at the far end of it in a session of its own.
/// One dialect stands for each kind of machine, since only the numbers differ between them.
/// </summary>
public sealed class PosixTtys : Ttys
{
    private readonly Numbers _numbers;

    private PosixTtys(Numbers numbers) => _numbers = numbers;

    /// <summary>The Linux one.</summary>
    public static PosixTtys Linux { get; } = new(Numbers.Linux);

    /// <inheritdoc/>
    public override bool Works => true;

    /// <inheritdoc/>
    public override Tty? Open(string command, string folder) => PosixTty.Open(_numbers, command, folder);
}
