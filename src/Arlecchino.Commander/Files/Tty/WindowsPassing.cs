using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Arlecchino.Commander.Files.Tty;

/// <summary>
/// Carries a Windows console through to a program that has claimed the screen, and back. While it runs the
/// application is a pane of glass: what is drawn and what is typed pass through it untouched.
/// </summary>
internal static class WindowsPassing
{
    /// <summary>How much is carried across at once.</summary>
    private const int Room = 32768;

    /// <summary>How often the wait gives up and looks at the window, in milliseconds.</summary>
    private const int Glance = 100;

    /// <summary>How long the two carriers are given to notice they are done, in milliseconds.</summary>
    private const int Parting = 1000;

    /// <summary>
    /// The key put into the real terminal's keyboard to let go of a read that is waiting for good. It is
    /// the space, whose letter and whose key are the one number, and whatever it reads is thrown away.
    /// </summary>
    private const ushort Space = 0x20;

    /// <summary>
    /// Runs until the program lets go. What it had already written goes out first, the instruction that
    /// claimed the screen among it, since the program wrote all of that to a terminal it is owed.
    /// </summary>
    /// <param name="terminal">The made terminal the program is at the far end of.</param>
    /// <param name="backlog">What the program printed that this application has not passed on.</param>
    /// <param name="count">How much of that there is.</param>
    internal static void Between(WindowsTty terminal, byte[] backlog, int count)
    {
        var keyboard = Windows.Stream(Windows.Keyboard);
        var screen = Windows.Stream(Windows.Screen);
        bool typed = Windows.ReadModes(keyboard, out var typing);
        bool drawn = Windows.ReadModes(screen, out var drawing);
        var keyboardPage = Windows.KeyboardPage();
        var screenPage = Windows.ScreenPage();
        var carrying = Marshal.AllocHGlobal(Room);
        var ending = new ManualResetEventSlim(false);

        try
        {
            Raw(keyboard, screen, typed, typing, drawn, drawing);

            if (count > 0)
            {
                var owed = Math.Min(count, Room);

                Marshal.Copy(backlog, 0, carrying, owed);
                _ = Windows.WriteAll(screen, carrying, owed);
            }

            Carry(terminal, keyboard, screen, ending);
        }
        finally
        {
            if (typed)
            {
                _ = Windows.WriteModes(keyboard, typing);
            }

            if (drawn)
            {
                _ = Windows.WriteModes(screen, drawing);
            }

            _ = Windows.SetKeyboardPage(keyboardPage);
            _ = Windows.SetScreenPage(screenPage);

            ending.Dispose();
            Marshal.FreeHGlobal(carrying);
        }
    }

    /// <summary>
    /// Carries both ways at once and watches the window while it does. Neither carrier can be waited on
    /// and looked away from at the same time, so each has a thread and the window is watched from here.
    /// </summary>
    /// <param name="terminal">The made terminal.</param>
    /// <param name="keyboard">The real terminal's keyboard.</param>
    /// <param name="screen">The real terminal's screen.</param>
    /// <param name="ending">Set once the program has no more to say, which is the end of the loan.</param>
    private static void Carry(WindowsTty terminal, IntPtr keyboard, IntPtr screen, ManualResetEventSlim ending)
    {
        var drawing = new Thread(() =>
        {
            Draws(terminal, screen);
            ending.Set();
        })
        {
            IsBackground = true,
            Name = "arlc-draws",
        };

        var typing = new Thread(() => Types(terminal, keyboard, ending))
        {
            IsBackground = true,
            Name = "arlc-types",
        };

        drawing.Start();
        typing.Start();

        var window = WindowsTty.Asked();

        while (!ending.Wait(Glance))
        {
            Resized(terminal, ref window);
        }

        Wake(keyboard);

        _ = drawing.Join(Parting);
        _ = typing.Join(Parting);
    }

    /// <summary>Takes what the program draws and gives it to the real screen, until it has no more.</summary>
    /// <param name="terminal">The made terminal.</param>
    /// <param name="screen">The real terminal's screen.</param>
    private static void Draws(WindowsTty terminal, IntPtr screen)
    {
        var carrying = Marshal.AllocHGlobal(Room);

        try
        {
            while (true)
            {
                var count = terminal.Taken(carrying, Room);

                if (count <= 0 || !Windows.WriteAll(screen, carrying, count))
                {
                    return;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(carrying);
        }
    }

    /// <summary>Takes what is typed at the real terminal and gives it to the program.</summary>
    /// <param name="terminal">The made terminal.</param>
    /// <param name="keyboard">The real terminal's keyboard.</param>
    /// <param name="ending">Watched, so that the key that lets this read go is not passed on.</param>
    private static void Types(WindowsTty terminal, IntPtr keyboard, ManualResetEventSlim ending)
    {
        var carrying = Marshal.AllocHGlobal(Room);

        try
        {
            while (!ending.IsSet)
            {
                var count = Windows.ReadOnce(keyboard, carrying, Room);

                if (count <= 0 || ending.IsSet || !terminal.Fed(carrying, count))
                {
                    return;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(carrying);
        }
    }

    /// <summary>
    /// Puts the real terminal into the raw modes, and into the code page the carried bytes are spelled
    /// in. The gathering into lines, the echo and the signals all belong to the program now.
    /// </summary>
    /// <param name="keyboard">The real terminal's keyboard.</param>
    /// <param name="screen">The real terminal's screen.</param>
    /// <param name="typed">Whether the keyboard would say what modes it is in.</param>
    /// <param name="typing">Those modes, which are left alone to be put back.</param>
    /// <param name="drawn">Whether the screen would say what modes it is in.</param>
    /// <param name="drawing">Those modes, likewise.</param>
    private static void Raw(IntPtr keyboard, IntPtr screen, bool typed, uint typing, bool drawn, uint drawing)
    {
        if (typed)
        {
            _ = Windows.WriteModes(
                keyboard,
                (typing & ~(Windows.Lines | Windows.Echoes | Windows.Signals | Windows.Selecting)) |
                Windows.Sequences |
                Windows.Mouse |
                Windows.TheseFlags);
        }

        if (drawn)
        {
            _ = Windows.WriteModes(screen, drawing | Windows.Draws | Windows.NoReturns);
        }

        _ = Windows.SetKeyboardPage(Windows.Utf8);
        _ = Windows.SetScreenPage(Windows.Utf8);
    }

    /// <summary>Tells the program about a window that has changed size, and nothing when it has not.</summary>
    /// <param name="terminal">The made terminal.</param>
    /// <param name="window">The window as it was, left as it now is.</param>
    private static void Resized(WindowsTty terminal, ref Windows.Size window)
    {
        var size = WindowsTty.Asked();

        if (size.Columns == window.Columns && size.Rows == window.Rows)
        {
            return;
        }

        window = size;
        terminal.Resize(size.Columns, size.Rows);
    }

    /// <summary>
    /// Lets go of the read that is waiting on the keyboard. Nothing about a console will interrupt one, so
    /// a key is put into the keyboard instead; the carrier is already done and throws whatever it reads away.
    /// </summary>
    /// <param name="keyboard">The real terminal's keyboard.</param>
    private static void Wake(IntPtr keyboard)
    {
        var press = new Windows.Press
        {
            Kind = Windows.KeyDown,
            Down = 1,
            Times = 1,
            Key = Space,
            Letter = Space,
        };

        _ = Windows.Wake(keyboard, ref press, 1, out _);
    }
}
