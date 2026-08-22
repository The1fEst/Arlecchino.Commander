using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Arlecchino.Commander.Files.Tty;

namespace Arlecchino.Commander.Tests.Carries;

/// <summary>
/// One handover, run inside a terminal that has just been opened for it. The test opens the terminal,
/// starts this in it, and reads back what this wrote down.
/// </summary>
internal static class Program
{
    /// <summary>What the program exits with when the key that reached it was the key that was pressed.</summary>
    internal const int Answer = 7;

    /// <summary>The key pressed at the real terminal once the program has the screen.</summary>
    private const char TypedLetter = 'q';

    /// <summary>How long the program is given to settle into the screen before the key, in milliseconds.</summary>
    private const int Settling = 400;

    /// <summary>The keyboard of the real terminal, as this machine numbers the three streams.</summary>
    private const int Keyboard = -10;

    /// <summary>The screen of the real terminal.</summary>
    private const int Screen = -11;

    /// <summary>How much is taken off the made terminal at once.</summary>
    private const int Mouthful = 32768;

    /// <summary>
    /// A program that takes the screen, waits to be told something, and says whether it was told the right
    /// thing. Nothing has to be installed for it: every machine that has this terminal has PowerShell.
    /// </summary>
    private static string Drawing =>
        "powershell -NoProfile -c \"[Console]::Write([char]27 + '[?1049h'); " +
        "Write-Host 'the screen belongs to this program now'; $key = [Console]::ReadKey($true); " +
        "[Console]::Write([char]27 + '[?1049l'); " +
        $"if ($key.KeyChar -eq '{TypedLetter}') {{ exit {Answer} }} else {{ exit 8 }}\"";

    /// <summary>Runs the one handover and writes down what came of it.</summary>
    /// <param name="args">Where to write it, and what the terminal and the shell around it are called.</param>
    /// <returns>Nought when the screen went to the program and came back.</returns>
    private static int Main(string[] args)
    {
        var log = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "carries.log");
        var lines = new StringBuilder();

        lines.AppendLine($"terminal={(args.Length > 1 ? args[1] : "?")}");
        lines.AppendLine($"shell={(args.Length > 2 ? args[2] : "?")}");
        lines.AppendLine($"host={Host()}");

        var keyboard = GetStdHandle(Keyboard);
        var screen = GetStdHandle(Screen);
        bool read = GetConsoleMode(keyboard, out var typing);
        bool drawn = GetConsoleMode(screen, out var drawing);
        var keyboardPage = GetConsoleCP();
        var screenPage = GetConsoleOutputCP();

        lines.AppendLine(Answered(read, drawn));
        lines.AppendLine($"before={typing:X4}/{drawing:X4} pages={keyboardPage}/{screenPage}");

        using var terminal = Ttys.Local.Open(Drawing, Directory.GetCurrentDirectory());

        if (terminal is null || Owed(terminal) is not { } owed)
        {
            lines.AppendLine("claimed=no");

            return Done(log, lines, 1);
        }

        lines.AppendLine("claimed=yes");
        lines.AppendLine($"owed={owed.Length}");

        var pressing = new Thread(() =>
        {
            Thread.Sleep(Settling);
            Press(keyboard);
        })
        {
            IsBackground = true,
        };

        pressing.Start();
        terminal.Carry(owed, owed.Length);

        lines.AppendLine($"outcome={terminal.Wait()}");

        _ = GetConsoleMode(keyboard, out var typingNow);
        _ = GetConsoleMode(screen, out var drawingNow);

        lines.AppendLine($"after={typingNow:X4}/{drawingNow:X4} pages={GetConsoleCP()}/{GetConsoleOutputCP()}");
        lines.AppendLine((!read || typing == typingNow) && (!drawn || drawing == drawingNow)
            ? "modes=put back"
            : "modes=left changed");
        lines.AppendLine(keyboardPage == GetConsoleCP() && screenPage == GetConsoleOutputCP()
            ? "pages=put back"
            : "pages=left changed");

        return Done(log, lines, 0);
    }

    /// <summary>
    /// Whether there is a terminal here at all, which the modes answer only where there is one to ask.
    /// A console never opened is worth telling apart from a handover that went wrong.
    /// </summary>
    /// <param name="read">Whether the keyboard said what modes it is in.</param>
    /// <param name="drawn">Whether the screen did.</param>
    /// <returns>The line to write down.</returns>
    private static string Answered(bool read, bool drawn) => read && drawn ? "console=yes" : "console=no";

    /// <summary>
    /// Reads the program until it asks for the screen, keeping what it is owed: the instruction it asked
    /// with, and whatever it had written behind that instruction in the same mouthful.
    /// </summary>
    /// <param name="terminal">The made terminal.</param>
    /// <returns>What the program is owed, or <c>null</c> when it ended without asking for anything.</returns>
    private static byte[]? Owed(Tty terminal)
    {
        var claims = new Claims(terminal.Blanks);
        var mouthful = new byte[Mouthful];
        var owed = new List<byte>();

        while (true)
        {
            var count = terminal.Read(mouthful);

            if (count <= 0)
            {
                return null;
            }

            for (var at = 0; at < count; at++)
            {
                if (claims.Takes(mouthful[at]) is not Sign.Screen)
                {
                    continue;
                }

                owed.AddRange(claims.Sequence);

                for (var rest = at + 1; rest < count; rest++)
                {
                    owed.Add(mouthful[rest]);
                }

                return [.. owed];
            }
        }
    }

    /// <summary>
    /// Presses a key at the real terminal, as the person watching it would. It goes into the keyboard the
    /// handover is carrying, so a program that answers to it is a program the keyboard is reaching.
    /// </summary>
    /// <param name="keyboard">The real terminal's keyboard.</param>
    private static void Press(IntPtr keyboard)
    {
        var press = new Record
        {
            Kind = 1,
            Down = 1,
            Times = 1,
            Letter = TypedLetter,
        };

        _ = WriteConsoleInputW(keyboard, ref press, 1, out _);

        press.Down = 0;

        _ = WriteConsoleInputW(keyboard, ref press, 1, out _);
    }

    /// <summary>Which terminal this turned out to be running in, as the terminal itself says.</summary>
    /// <returns>The name it goes by.</returns>
    private static string Host()
    {
        if (Environment.GetEnvironmentVariable("WT_SESSION") is { Length: > 0 })
        {
            return "Windows Terminal";
        }

        return Environment.GetEnvironmentVariable("TERM_PROGRAM") is { Length: > 0 } name ? name : "?";
    }

    /// <summary>Writes the log, whose last line is what says it is whole rather than half written.</summary>
    /// <param name="log">Where to write.</param>
    /// <param name="lines">What to write.</param>
    /// <param name="outcome">What to exit with.</param>
    /// <returns>The outcome, so that this can be returned from where it is called.</returns>
    private static int Done(string log, StringBuilder lines, int outcome)
    {
        lines.AppendLine("done");

        File.WriteAllText(log, lines.ToString());

        return outcome;
    }

    /// <summary>One event in a console's keyboard, of the one kind that is ever written back into it.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Record
    {
        /// <summary>Which kind of event this is.</summary>
        public ushort Kind;

        /// <summary>Room the machine leaves between the kind and the rest.</summary>
        public ushort Gap;

        /// <summary>Whether the key is going down rather than coming up.</summary>
        public int Down;

        /// <summary>How many times over.</summary>
        public ushort Times;

        /// <summary>Which key it is, which nothing here says.</summary>
        public ushort Key;

        /// <summary>Where on the keyboard it sits, which nothing here says either.</summary>
        public ushort Place;

        /// <summary>What letter it stands for.</summary>
        public ushort Letter;

        /// <summary>Which of shift, control and the rest were held with it.</summary>
        public uint HeldKeys;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int number);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr handle, out uint modes);

    [DllImport("kernel32.dll")]
    private static extern uint GetConsoleCP();

    [DllImport("kernel32.dll")]
    private static extern uint GetConsoleOutputCP();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool WriteConsoleInputW(IntPtr handle, ref Record press, uint count, out uint part);
}
