using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Arlecchino.Commander.Tests.Carries;

/// <summary>
/// The terminal a program of this kind is opened in: a pair of ends, whose state is the modes of the
/// line discipline. Where those modes sit is the machine's, so each machine is a class of its own.
/// </summary>
internal abstract class PosixTerminal : RealTerminal
{
    /// <summary>The keyboard of the real terminal.</summary>
    private const int Keyboard = 0;

    /// <summary>The screen of the real terminal.</summary>
    private const int Screen = 1;

    /// <summary>Room enough for the modes of any terminal, which are read whole.</summary>
    private const int ModesRoom = 128;

    /// <summary>How many sets of flags there are, which stand one after another from the beginning.</summary>
    private const int FlagsCount = 4;

    /// <summary>Which of the four sets of flags is the line's.</summary>
    private const int LineFlags = 3;

    /// <inheritdoc/>
    internal override bool Works => IsTerminal(Keyboard) == 1 && IsTerminal(Screen) == 1;

    /// <summary>
    /// Asked of the shell every machine of this kind has, rather than of the one the person here uses:
    /// the words below are the POSIX ones, and a shell of another sort would not read them.
    /// </summary>
    internal override string Drawing =>
        "sh -c 'stty raw -echo; printf \"\\033[?1049h\"; " +
        $"printf \"{Program.Marker}\\r\\n\"; " +
        "given=$(dd bs=1 count=1 2>/dev/null); stty sane; printf \"\\033[?1049l\"; " +
        $"[ \"$given\" = \"{Program.TypedLetter}\" ] && exit {Program.Answer}; exit 8'";

    /// <summary>
    /// The two flags among the line's that the machine keeps for itself: what it has yet to write out,
    /// and what it has yet to show again. They turn as a person types and say nothing about the setting.
    /// </summary>
    private protected abstract ulong ItsOwnFlags { get; }

    /// <summary>Where the letters that stand for the keys with a meaning of their own sit.</summary>
    private protected abstract int LettersAt { get; }

    /// <summary>How many of them there are.</summary>
    private protected abstract int LettersCount { get; }

    /// <summary>
    /// The four sets of flags and the letters, in one line. How wide a set is and where the letters
    /// begin is the machine's; that there are four sets and that the last of them is the line's is not.
    /// </summary>
    /// <returns>The line.</returns>
    internal override string State()
    {
        var modes = Marshal.AllocHGlobal(ModesRoom);

        try
        {
            if (ReadModes(Keyboard, modes) != 0)
            {
                return "none";
            }

            var state = new StringBuilder();

            for (var flags = 0; flags < FlagsCount; flags++)
            {
                var set = Flags(modes, flags);

                _ = state.Append((flags == LineFlags ? set & ~ItsOwnFlags : set).ToString("X8", null)).Append('/');
            }

            for (var letter = 0; letter < LettersCount; letter++)
            {
                _ = state.Append(Marshal.ReadByte(modes, LettersAt + letter).ToString("X2", null));
            }

            return state.ToString();
        }
        finally
        {
            Marshal.FreeHGlobal(modes);
        }
    }

    /// <summary>
    /// Nothing. The key is pressed by whoever opened the terminal — a window told through the name it
    /// listens on, a pair of ends typed into at the near end — so that the try itself changes nothing.
    /// </summary>
    /// <param name="letter">The key, which is pressed elsewhere.</param>
    internal override void Presses(char letter) { }

    /// <summary>One of the four sets of flags, read as wide as this machine keeps them.</summary>
    /// <param name="modes">The modes, read whole.</param>
    /// <param name="flags">Which set.</param>
    /// <returns>The set.</returns>
    private protected abstract ulong Flags(IntPtr modes, int flags);

    /// <summary>Whether one of the three streams is a terminal rather than a pipe.</summary>
    /// <param name="handle">The stream.</param>
    /// <returns>One when it is.</returns>
    [DllImport("libc", EntryPoint = "isatty", SetLastError = true)]
    private static extern int IsTerminal(int handle);

    /// <summary>Reads the modes a terminal is in.</summary>
    /// <param name="handle">The terminal.</param>
    /// <param name="modes">Where to put them.</param>
    /// <returns>Nought when it worked.</returns>
    [DllImport("libc", EntryPoint = "tcgetattr", SetLastError = true)]
    private static extern int ReadModes(int handle, IntPtr modes);
}
