using System;
using System.Runtime.InteropServices;

namespace Arlecchino.Commander.Tests.Carries;

/// <summary>
/// Where this machine keeps the modes of a terminal: four sets of flags a word wide, and the letters
/// just past them with a byte of the machine's own in between.
/// </summary>
internal sealed class LinuxTerminal : PosixTerminal
{
    /// <summary>How wide one of the four sets of flags is here.</summary>
    private const int FlagsRoom = 4;

    /// <inheritdoc/>
    private protected override ulong ItsOwnFlags => 0x5000;

    /// <inheritdoc/>
    private protected override int LettersAt => 17;

    /// <inheritdoc/>
    private protected override int LettersCount => 32;

    /// <inheritdoc/>
    private protected override ulong Flags(IntPtr modes, int flags) =>
        (uint)Marshal.ReadInt32(modes, flags * FlagsRoom);
}
