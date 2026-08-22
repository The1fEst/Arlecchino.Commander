using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Arlecchino.Commander.Files.Ssh;

namespace Arlecchino.Commander.Files.Tty;

/// <summary>
/// A terminal made the POSIX way. The command at the far end of it has a session and a terminal of its
/// own, which is what makes <c>Ctrl+C</c> reach it rather than us.
/// </summary>
public sealed class PosixTty : Tty
{
    /// <summary>How much is taken off the terminal at once.</summary>
    private const int Mouthful = 32768;

    /// <summary>Ask whether the command has ended rather than wait for it to.</summary>
    private const int WithoutWaiting = 1;

    /// <summary>The window a terminal is given when the real one will not say how large it is.</summary>
    private const int SomeColumns = 80;

    /// <summary>The window a terminal is given when the real one will not say how large it is.</summary>
    private const int SomeRows = 24;

    private readonly Lock _lock = new();
    private readonly IntPtr _taking = Marshal.AllocHGlobal(Mouthful);
    private readonly IntPtr _giving = Marshal.AllocHGlobal(Mouthful);
    private readonly Numbers _numbers;
    private readonly int _child;

    private int _end;
    private bool _ended;
    private int _outcome;

    private PosixTty(Numbers numbers, int end, int child)
    {
        _numbers = numbers;
        _end = end;
        _child = child;
    }

    /// <summary>The near end of the pair, for waiting on it alongside the keyboard.</summary>
    internal int End => _end;

    /// <summary>Nothing is painted here: a pair of ends is silent until the command speaks.</summary>
    public override bool Blanks => false;

    /// <summary>Nothing is written back either, the pair having been hushed when it was opened.</summary>
    public override bool Echoes => false;

    /// <summary>The newline, which is what the line discipline of a pair gathers a line up to.</summary>
    public override byte Enter => (byte)'\n';

    /// <inheritdoc/>
    public override bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                if (_ended)
                {
                    return false;
                }

                if (Posix.Waited(_child, out var status, WithoutWaiting) != _child)
                {
                    return true;
                }

                _ended = true;
                _outcome = Posix.Outcome(status);

                return false;
            }
        }
    }

    /// <summary>Opens a pair and starts the command at the far end of it.</summary>
    /// <param name="numbers">What this kind of machine takes.</param>
    /// <param name="command">What was typed.</param>
    /// <param name="folder">The folder to run it in.</param>
    /// <returns>The terminal, or <c>null</c> when none could be had.</returns>
    public static PosixTty? Open(Numbers numbers, string command, string folder)
    {
        var end = Posix.OpenTerminal(Posix.ReadWrite | numbers.NotMine);

        if (end < 0)
        {
            return null;
        }

        if (Posix.Grant(end) != 0 ||
            Posix.Unlock(end) != 0 ||
            Marshal.PtrToStringUTF8(Posix.FarEnd(end)) is not { Length: > 0 } far)
        {
            _ = Posix.Close(end);

            return null;
        }

        Hush(numbers, end);

        var window = Asked(numbers);
        var child = Start(numbers, far, command, folder, window);

        if (child <= 0)
        {
            _ = Posix.Close(end);

            return null;
        }

        var terminal = new PosixTty(numbers, end, child);

        terminal.Resize(window.Columns, window.Rows);

        return terminal;
    }

    /// <summary>How large the real terminal's window is, which the made one is given to begin with.</summary>
    /// <param name="numbers">What this kind of machine takes.</param>
    /// <returns>The window.</returns>
    internal static Posix.Window Asked(Numbers numbers)
    {
        var window = default(Posix.Window);

        if (Posix.Sizing(1, numbers.AsksWindow, ref window) == 0 && window is { Rows: > 0, Columns: > 0 })
        {
            return window;
        }

        return new() { Columns = SomeColumns, Rows = SomeRows };
    }

    /// <summary>How large the real terminal's window is now.</summary>
    /// <returns>The window.</returns>
    internal Posix.Window Real() => Asked(_numbers);

    /// <inheritdoc/>
    public override int Read(byte[] buffer)
    {
        var end = _end;

        if (end < 0)
        {
            return 0;
        }

        var count = Posix.ReadOnce(end, _taking, Math.Min(buffer.Length, Mouthful));

        if (count > 0)
        {
            Marshal.Copy(_taking, buffer, 0, count);
        }

        return count;
    }

    /// <inheritdoc/>
    public override bool Write(byte[] bytes, int count)
    {
        lock (_lock)
        {
            if (_end < 0 || count <= 0 || count > Mouthful)
            {
                return false;
            }

            Marshal.Copy(bytes, 0, _giving, count);

            return Posix.WriteAll(_end, _giving, count);
        }
    }

    /// <inheritdoc/>
    public override void Resize(int columns, int rows)
    {
        lock (_lock)
        {
            if (_end < 0)
            {
                return;
            }

            var window = new Posix.Window
            {
                Columns = (ushort)Math.Clamp(columns, 1, ushort.MaxValue),
                Rows = (ushort)Math.Clamp(rows, 1, ushort.MaxValue),
            };

            _ = Posix.Sizing(_end, _numbers.TellsWindow, ref window);
        }
    }

    /// <inheritdoc/>
    public override int Wait()
    {
        lock (_lock)
        {
            while (!_ended)
            {
                if (Posix.Waited(_child, out var status, 0) == _child)
                {
                    _ended = true;
                    _outcome = Posix.Outcome(status);

                    break;
                }

                if (Marshal.GetLastPInvokeError() == Posix.Interruption)
                {
                    continue;
                }

                _ended = true;
                _outcome = -1;
            }

            return _outcome;
        }
    }

    /// <summary>
    /// Asks the command to end, and everything it started with it. They are all in the one session, so
    /// the signal goes to the group rather than to the command alone.
    /// </summary>
    /// <returns><c>true</c> when there was something to ask.</returns>
    public override bool Interrupt() =>
        Posix.Signal(-_child, Posix.Terminate) == 0 || Posix.Signal(_child, Posix.Terminate) == 0;

    /// <inheritdoc/>
    public override void Carry(byte[] backlog, int count) => Passing.Between(this, backlog, count);

    /// <inheritdoc/>
    public override void Dispose()
    {
        lock (_lock)
        {
            if (_end >= 0)
            {
                _ = Posix.Close(_end);
                _end = -1;
            }
        }

        Marshal.FreeHGlobal(_taking);
        Marshal.FreeHGlobal(_giving);
    }

    /// <summary>
    /// Stops the pair writing back whatever is typed at it. What is typed here is answered into a dialog
    /// and is a password more often than not, and echoed it would land in the roll for anyone to read.
    /// </summary>
    /// <param name="numbers">What this kind of machine takes.</param>
    /// <param name="end">The near end, through which the pair's modes are reached.</param>
    private static void Hush(Numbers numbers, int end)
    {
        var modes = Marshal.AllocHGlobal(Posix.ModesRoom);

        try
        {
            if (Posix.ReadModes(end, modes) != 0)
            {
                return;
            }

            var line = (uint)Marshal.ReadInt32(modes, numbers.LineModesAt);

            Marshal.WriteInt32(modes, numbers.LineModesAt, (int)(line & ~Posix.Echoes));
            _ = Posix.WriteModes(end, Posix.Instant, modes);
        }
        finally
        {
            Marshal.FreeHGlobal(modes);
        }
    }

    /// <summary>
    /// Starts the shell with the command in it, with the far end of the pair for all three of its streams.
    /// A library too old to know how to make a session is asked again without one.
    /// </summary>
    /// <param name="numbers">What this kind of machine takes.</param>
    /// <param name="far">What the far end of the pair is called.</param>
    /// <param name="command">What was typed.</param>
    /// <param name="folder">The folder to run it in.</param>
    /// <param name="window">How large to say the window is.</param>
    /// <returns>The started program, or nought when nothing started.</returns>
    private static int Start(Numbers numbers, string far, string command, string folder, Posix.Window window)
    {
        var blocks = new List<IntPtr>();
        var actions = Marshal.AllocHGlobal(Posix.ModesRoom * 8);
        var attributes = Marshal.AllocHGlobal(Posix.ModesRoom * 8);

        try
        {
            if (Posix.OpenActions(actions) != 0)
            {
                return 0;
            }

            if (Posix.OpenAttributes(attributes) != 0)
            {
                _ = Posix.DropActions(actions);

                return 0;
            }

            var path = Posix.Held(far);

            blocks.Add(path);

            if (Posix.ActionOpens(actions, 0, path, Posix.ReadWrite, 0) != 0 ||
                Posix.ActionPoints(actions, 0, 1) != 0 ||
                Posix.ActionPoints(actions, 0, 2) != 0)
            {
                return 0;
            }

            var shell = Posix.Held(Which());
            var words = Posix.HeldList(Words(command, folder), blocks);
            var environment = Posix.HeldList(Around(window), blocks);

            blocks.Add(shell);

            if (Posix.Attribute(attributes, numbers.OwnSession) == 0 &&
                Posix.Spawn(out var child, shell, actions, attributes, words, environment) == 0)
            {
                return child;
            }

            _ = Posix.Attribute(attributes, 0);

            return Posix.Spawn(out var alone, shell, actions, attributes, words, environment) == 0 ? alone : 0;
        }
        finally
        {
            _ = Posix.DropActions(actions);
            _ = Posix.DropAttributes(attributes);
            Marshal.FreeHGlobal(actions);
            Marshal.FreeHGlobal(attributes);
            Posix.Free(blocks);
        }
    }

    /// <summary>The shell of the person sitting here, or the one every machine has.</summary>
    /// <returns>The path to it.</returns>
    private static string Which() =>
        Environment.GetEnvironmentVariable("SHELL") is { Length: > 0 } shell && File.Exists(shell)
            ? shell
            : "/bin/sh";

    /// <summary>
    /// The shell and what to tell it. The folder is walked into by the command, since starting a program
    /// somewhere is not among the things a spawn can be asked.
    /// </summary>
    /// <param name="command">What was typed.</param>
    /// <param name="folder">The folder to run it in.</param>
    /// <returns>The words, of which the first is the shell itself.</returns>
    private static List<string> Words(string command, string folder)
    {
        var place = Directory.Exists(folder) ? folder : Environment.CurrentDirectory;

        return [Which(), "-c", PosixShell.Instance.Within(place, command)];
    }

    /// <summary>
    /// The environment the command runs in, which is ours with the window written into it. A terminal
    /// with no name is given one, since a program that cannot tell what terminal it is at draws nothing.
    /// </summary>
    /// <param name="window">How large the window is.</param>
    /// <returns>The environment, each entry spelled as the library wants it.</returns>
    private static List<string> Around(Posix.Window window)
    {
        var entries = new List<string>();
        var named = false;

        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var name = entry.Key.ToString() ?? "";

            if (name.Length == 0 || name is "COLUMNS" or "LINES")
            {
                continue;
            }

            named |= name == "TERM";
            entries.Add($"{name}={entry.Value}");
        }

        if (!named)
        {
            entries.Add("TERM=xterm-256color");
        }

        entries.Add($"COLUMNS={window.Columns}");
        entries.Add($"LINES={window.Rows}");

        return entries;
    }
}
