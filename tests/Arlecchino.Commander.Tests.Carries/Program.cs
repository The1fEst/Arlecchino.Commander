using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Arlecchino.Commander.Files.Tty;

namespace Arlecchino.Commander.Tests.Carries;

/// <summary>
/// One handover, run inside a terminal that has just been opened for it. The test opens the terminal,
/// starts this in it, and reads back what this wrote down; what differs by machine is
/// <see cref="RealTerminal"/>.
/// </summary>
internal static class Program
{
    /// <summary>What the program exits with when the key that reached it was the key that was pressed.</summary>
    internal const int Answer = 7;

    /// <summary>The key pressed at the real terminal once the program has the screen.</summary>
    internal const char TypedLetter = 'q';

    /// <summary>What the program that took the screen says once it has it.</summary>
    internal const string Marker = "the screen belongs to this program now";

    /// <summary>How long the program is given to settle into the screen before the key, in milliseconds.</summary>
    private const int DelayMilliseconds = 400;

    /// <summary>How much is taken off the made terminal at once.</summary>
    private const int Mouthful = 32768;

    /// <summary>Runs the one handover and writes down what came of it.</summary>
    /// <param name="args">Where to write it, and what the terminal and the shell it runs in are called.</param>
    /// <returns>Nought when the screen went to the program and came back.</returns>
    private static int Main(string[] args)
    {
        var roll = new Roll(args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "carries.log"));
        var realTerminal = RealTerminal.OfThisMachine();

        roll.Says($"terminal={(args.Length > 1 ? args[1] : "?")}");
        roll.Says($"shell={(args.Length > 2 ? args[2] : "?")}");
        roll.Says($"host={Host()}");
        roll.Says(realTerminal.Works ? "console=yes" : "console=no");

        var firstState = realTerminal.State();

        roll.Says($"before={firstState}");

        using var terminal = Ttys.Local.Open(realTerminal.Drawing, Directory.GetCurrentDirectory());

        if (terminal is null || Owed(terminal) is not { } owed)
        {
            roll.Says("claimed=no");

            return Done(roll, 1);
        }

        roll.Says($"owed={owed.Length}");
        roll.Says("claimed=yes");

        Pressing(realTerminal).Start();
        terminal.Carry(owed, owed.Length);

        roll.Says($"outcome={terminal.Wait()}");

        var lastState = realTerminal.State();

        roll.Says($"after={lastState}");
        roll.Says(firstState == lastState ? "modes=put back" : "modes=left changed");

        return Done(roll, 0);
    }

    /// <summary>
    /// Presses the key once the program that took the screen has had a moment to ask for one. It is a
    /// thread of its own because the handover holds the one it is started from until the program ends.
    /// </summary>
    /// <param name="realTerminal">The real terminal, which is what the key is pressed at.</param>
    /// <returns>The thread, not yet started.</returns>
    private static Thread Pressing(RealTerminal realTerminal) => new(() =>
    {
        Thread.Sleep(DelayMilliseconds);
        realTerminal.Presses(TypedLetter);
    })
    {
        IsBackground = true,
    };

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

    /// <summary>Which terminal this turned out to be running in, as the terminal itself says.</summary>
    /// <returns>The name it goes by.</returns>
    private static string Host()
    {
        if (Environment.GetEnvironmentVariable("WT_SESSION") is { Length: > 0 })
        {
            return "Windows Terminal";
        }

        if (Environment.GetEnvironmentVariable("TERM_PROGRAM") is { Length: > 0 } named)
        {
            return named;
        }

        return Environment.GetEnvironmentVariable("TERM") is { Length: > 0 } kind ? kind : "?";
    }

    /// <summary>Writes the last line, which is what says the roll is whole rather than half written.</summary>
    /// <param name="roll">The roll the try has been writing.</param>
    /// <param name="outcome">What to exit with.</param>
    /// <returns>The outcome, so that this can be returned from where it is called.</returns>
    private static int Done(Roll roll, int outcome)
    {
        roll.Says("done");

        return outcome;
    }

    /// <summary>
    /// What the try writes down, kept on disk as each line is written rather than at the end. The test
    /// outside watches it for the line that says the screen has gone, and presses a key when it comes.
    /// </summary>
    private sealed class Roll
    {
        private readonly StringBuilder _lines = new();
        private readonly string _log;

        /// <summary>Puts the roll where the test is looking for it.</summary>
        /// <param name="log">Where to write.</param>
        internal Roll(string log) => _log = log;

        /// <summary>Writes one line down.</summary>
        /// <param name="line">The line.</param>
        internal void Says(string line)
        {
            _ = _lines.AppendLine(line);

            File.WriteAllText(_log, _lines.ToString());
        }
    }
}
