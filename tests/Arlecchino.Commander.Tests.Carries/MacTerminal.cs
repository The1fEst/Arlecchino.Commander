using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arlecchino.Commander.Tests.Carries;

/// <summary>
/// Where this machine keeps the modes of a terminal: four sets of flags twice a word wide, and the
/// letters a way past them.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MacTerminal : PosixTerminal
{
    /// <summary>How wide one of the four sets of flags is here.</summary>
    private const int FlagsRoom = 8;

    /// <inheritdoc/>
    private protected override ulong ItsOwnFlags => 0x20800000;

    /// <inheritdoc/>
    private protected override int LettersAt => 32;

    /// <inheritdoc/>
    private protected override int LettersCount => 20;

    /// <inheritdoc/>
    private protected override ulong Flags(IntPtr modes, int flags) =>
        (ulong)Marshal.ReadInt64(modes, flags * FlagsRoom);
}
