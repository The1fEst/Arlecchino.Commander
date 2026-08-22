using System.Runtime.InteropServices;

namespace Arlecchino.Commander.Files.Tty;

/// <summary>
/// What macOS takes, and how it is asked. The window is asked of the system underneath the library,
/// since the library spells that one call with an argument list this machine hands over on the stack.
/// </summary>
public sealed class MacTtys : PosixTtys
{
    /// <summary>The one of these there is.</summary>
    public static MacTtys Instance { get; } = new();

    private MacTtys() { }

    /// <inheritdoc/>
    internal override int NotMine => 0x20000;

    /// <inheritdoc/>
    internal override short OwnSession => 0x400;

    /// <inheritdoc/>
    internal override nuint AsksWindow => 0x40087468;

    /// <inheritdoc/>
    internal override nuint TellsWindow => 0x80087467;

    /// <inheritdoc/>
    internal override int LineModesAt => 24;

    /// <inheritdoc/>
    internal override int Sizing(int handle, nuint request, ref Posix.Window window) =>
        SystemSizing(handle, request, ref window);

    /// <summary>The call as the system takes it, underneath the library that spells it the other way.</summary>
    /// <param name="handle">The terminal.</param>
    /// <param name="request">Which of the two.</param>
    /// <param name="window">The size, read or written.</param>
    /// <returns>Nought when it worked.</returns>
    [DllImport(Posix.Library, EntryPoint = "__ioctl", SetLastError = true)]
    private static extern int SystemSizing(int handle, nuint request, ref Posix.Window window);
}
