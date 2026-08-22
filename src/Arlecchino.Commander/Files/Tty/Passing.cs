using System;
using System.Runtime.InteropServices;

namespace Arlecchino.Commander.Files.Tty;

/// <summary>
/// Carries a POSIX terminal through to a program that has claimed the screen, and back. While it runs the
/// application is a pane of glass: what is drawn and what is typed pass through it untouched.
/// </summary>
internal static class Passing
{
    /// <summary>How much is carried across at once.</summary>
    private const int Room = 32768;

    /// <summary>How often the wait gives up and looks around, in milliseconds.</summary>
    private const int Glance = 100;

    /// <summary>The keyboard of the real terminal.</summary>
    private const int Keyboard = 0;

    /// <summary>The screen of the real terminal.</summary>
    private const int Screen = 1;

    /// <summary>
    /// Runs until the program lets go. What it had already written goes out first, the instruction that
    /// claimed the screen among it, since the program wrote all of that to a terminal it is owed.
    /// </summary>
    /// <param name="terminal">The made terminal the program is at the far end of.</param>
    /// <param name="backlog">What the program printed that this application has not passed on.</param>
    /// <param name="count">How much of that there is.</param>
    internal static void Between(PosixTty terminal, byte[] backlog, int count)
    {
        var carrying = Marshal.AllocHGlobal(Room);
        var modes = Marshal.AllocHGlobal(Posix.ModesRoom);
        var raw = Marshal.AllocHGlobal(Posix.ModesRoom);
        var kept = Posix.ReadModes(Keyboard, modes) == 0;

        try
        {
            if (count > 0)
            {
                Marshal.Copy(backlog, 0, carrying, Math.Min(count, Room));
                _ = Posix.WriteAll(Screen, carrying, Math.Min(count, Room));
            }

            if (kept)
            {
                Raw(modes, raw);
            }

            Carry(terminal, carrying);
        }
        finally
        {
            if (kept)
            {
                _ = Posix.WriteModes(Keyboard, Posix.Instant, modes);
            }

            Marshal.FreeHGlobal(carrying);
            Marshal.FreeHGlobal(modes);
            Marshal.FreeHGlobal(raw);
        }
    }

    /// <summary>
    /// Puts the real terminal into the raw modes. Everything typed has to arrive as it was typed: the
    /// gathering into lines, the echo and the signals all belong to the made terminal now.
    /// </summary>
    /// <param name="modes">The modes it was in, which are left alone to be put back.</param>
    /// <param name="raw">Room to work the raw ones out in.</param>
    private static void Raw(IntPtr modes, IntPtr raw)
    {
        for (var at = 0; at < Posix.ModesRoom; at += 8)
        {
            Marshal.WriteInt64(raw, at, Marshal.ReadInt64(modes, at));
        }

        Posix.MakeRaw(raw);

        _ = Posix.WriteModes(Keyboard, Posix.Instant, raw);
    }

    /// <summary>
    /// Waits on the keyboard and on the program at once and carries whichever speaks over to the other.
    /// A wait that ends in neither is the moment to notice the window has been resized.
    /// </summary>
    /// <param name="terminal">The made terminal.</param>
    /// <param name="carrying">Room to carry bytes in.</param>
    private static void Carry(PosixTty terminal, IntPtr carrying)
    {
        var end = terminal.End;
        var window = terminal.Real();
        var watches = new[]
        {
            new Posix.Watch { Handle = Keyboard, Events = Posix.Readable },
            new Posix.Watch { Handle = end, Events = Posix.Readable },
        };

        while (true)
        {
            watches[0].Report = 0;
            watches[1].Report = 0;

            var awake = Posix.Poll(watches, 2, Glance);

            if (awake < 0 && Marshal.GetLastPInvokeError() != Posix.Interruption)
            {
                return;
            }

            if (awake == 0)
            {
                Resized(terminal, ref window);

                continue;
            }

            if ((watches[1].Report & (Posix.Readable | Posix.Hangup)) != 0 && !Onward(end, Screen, carrying))
            {
                return;
            }

            if ((watches[0].Report & Posix.Readable) != 0 && !Onward(Keyboard, end, carrying))
            {
                watches[0].Handle = -1;
            }
        }
    }

    /// <summary>Takes what one end has to say and gives it to the other.</summary>
    /// <param name="from">Where it comes from.</param>
    /// <param name="to">Where it goes.</param>
    /// <param name="carrying">Room to carry it in.</param>
    /// <returns><c>false</c> once there is no more coming.</returns>
    private static bool Onward(int from, int to, IntPtr carrying)
    {
        var count = Posix.ReadOnce(from, carrying, Room);

        return count > 0 && Posix.WriteAll(to, carrying, count);
    }

    /// <summary>Tells the program about a window that has changed size, and nothing when it has not.</summary>
    /// <param name="terminal">The made terminal.</param>
    /// <param name="window">The window as it was, left as it now is.</param>
    private static void Resized(PosixTty terminal, ref Posix.Window window)
    {
        var size = terminal.Real();

        if (size.Columns == window.Columns && size.Rows == window.Rows)
        {
            return;
        }

        window = size;
        terminal.Resize(size.Columns, size.Rows);
    }
}
