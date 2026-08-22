using System.Runtime.InteropServices;

namespace Arlecchino.Commander.Files.Tty;

/// <summary>
/// What Linux takes, and how it is asked. Every call a terminal of one's own is made of is reached here
/// through the library, the window among them.
/// </summary>
public sealed class LinuxTtys : PosixTtys
{
    /// <summary>The one of these there is.</summary>
    public static LinuxTtys Instance { get; } = new();

    private LinuxTtys() { }

    /// <inheritdoc/>
    internal override int NotMine => 0x100;

    /// <inheritdoc/>
    internal override short OwnSession => 0x80;

    /// <inheritdoc/>
    internal override nuint AsksWindow => 0x5413;

    /// <inheritdoc/>
    internal override nuint TellsWindow => 0x5414;

    /// <inheritdoc/>
    internal override int LineModesAt => 12;

    /// <inheritdoc/>
    internal override int Sizing(int handle, nuint request, ref Posix.Window window) =>
        LibrarySizing(handle, request, ref window);

    /// <summary>
    /// The library's own call. It is spelled with an argument list of no fixed length, and this machine
    /// hands such a list over where it hands every other argument, so it is reached as it is written.
    /// </summary>
    /// <param name="handle">The terminal.</param>
    /// <param name="request">Which of the two.</param>
    /// <param name="window">The size, read or written.</param>
    /// <returns>Nought when it worked.</returns>
    [DllImport(Posix.Library, EntryPoint = "ioctl", SetLastError = true)]
    private static extern int LibrarySizing(int handle, nuint request, ref Posix.Window window);
}
