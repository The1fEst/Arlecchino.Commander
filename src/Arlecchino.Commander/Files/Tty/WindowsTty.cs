using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Arlecchino.Commander.Files.Tty;

/// <summary>
/// A terminal made the Windows way. The command at the far end of it has a console of its own, which is
/// what makes a program that draws believe it is at a terminal and draw rather than give up.
/// </summary>
public sealed class WindowsTty : Tty
{
    /// <summary>How much is taken off the terminal at once.</summary>
    private const int Mouthful = 32768;

    /// <summary>The window a terminal is given when the real one will not say how large it is.</summary>
    private const int SomeColumns = 80;

    /// <summary>The window a terminal is given when the real one will not say how large it is.</summary>
    private const int SomeRows = 24;

    /// <summary>
    /// What a command that was asked to end is taken to have exited with. A terminal counts one that a
    /// signal ended from a hundred and twenty-eight; there are no signals here, so it is a plain failure.
    /// </summary>
    private const uint Stopped = 1;

    /// <summary>How long the console is given to let go of the command before its hold is dropped.</summary>
    private const int Parting = 2000;

    private readonly Lock _lock = new();
    private readonly IntPtr _taking = Marshal.AllocHGlobal(Mouthful);
    private readonly IntPtr _giving = Marshal.AllocHGlobal(Mouthful);
    private readonly ManualResetEventSlim _ending = new(false);

    private volatile bool _ended;
    private IntPtr _process;
    private IntPtr _console;
    private IntPtr _typing;
    private IntPtr _reading;
    private IntPtr _job;
    private int _outcome;

    private WindowsTty(IntPtr console, IntPtr typing, IntPtr reading, IntPtr job, IntPtr process)
    {
        _console = console;
        _typing = typing;
        _reading = reading;
        _job = job;
        _process = process;
    }

    /// <inheritdoc/>
    public override bool IsRunning => !_ended;

    /// <summary>
    /// A console the machine makes is a fresh screen, and the first thing it says is that the screen is
    /// blank and the cursor at the top of it. That is the console speaking, not the command in it.
    /// </summary>
    public override bool Blanks => true;

    /// <summary>
    /// A console writes back whatever is typed at it while a command is reading a line, and there is no
    /// asking it not to from out here: the modes it reads by are its own and the command's.
    /// </summary>
    public override bool Echoes => true;

    /// <summary>The return, which is the one thing a console counts as the end of a line typed at it.</summary>
    public override byte Enter => (byte)'\r';

    /// <summary>Makes a console of the machine's own and starts the command in it.</summary>
    /// <param name="command">What was typed.</param>
    /// <param name="folder">The folder to run it in.</param>
    /// <returns>The terminal, or <c>null</c> when none could be had.</returns>
    public static WindowsTty? Open(string command, string folder)
    {
        if (!Windows.MakePipe(out var hearing, out var typing, IntPtr.Zero, 0))
        {
            return null;
        }

        if (!Windows.MakePipe(out var reading, out var drawing, IntPtr.Zero, 0))
        {
            _ = Windows.Close(hearing);
            _ = Windows.Close(typing);

            return null;
        }

        var made = Windows.MakeConsole(Asked(), hearing, drawing, 0, out var console) == 0;

        Handed(hearing, drawing);

        if (!made)
        {
            _ = Windows.Close(typing);
            _ = Windows.Close(reading);

            return null;
        }

        var job = Job();
        var process = Start(console, job, command, folder);

        if (process == IntPtr.Zero)
        {
            Windows.DropConsole(console);
            _ = Windows.Close(typing);
            _ = Windows.Close(reading);

            if (job != IntPtr.Zero)
            {
                _ = Windows.Close(job);
            }

            return null;
        }

        var terminal = new WindowsTty(console, typing, reading, job, process);

        terminal.Watch();

        return terminal;
    }

    /// <summary>
    /// Lets go of the two ends the console was handed when it was made. It keeps copies of its own, and
    /// these are this application's, which nothing on this side reads or writes again.
    /// </summary>
    /// <param name="hearing">The end the console reads what is typed from.</param>
    /// <param name="drawing">The end it writes what is drawn to.</param>
    private static void Handed(IntPtr hearing, IntPtr drawing)
    {
        _ = Windows.Close(hearing);
        _ = Windows.Close(drawing);
    }

    /// <summary>How large the real terminal's window is, which the made one is given to begin with.</summary>
    /// <returns>The window.</returns>
    internal static Windows.Size Asked() =>
        Windows.Measured(Windows.Stream(Windows.Screen)) ??
        new Windows.Size { Columns = SomeColumns, Rows = SomeRows };

    /// <inheritdoc/>
    public override int Read(byte[] buffer)
    {
        var reading = Volatile.Read(ref _reading);

        if (reading == IntPtr.Zero)
        {
            return 0;
        }

        var count = Windows.ReadOnce(reading, _taking, Math.Min(buffer.Length, Mouthful));

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
            if (count <= 0 || count > Mouthful)
            {
                return false;
            }

            Marshal.Copy(bytes, 0, _giving, count);

            return Fed(_giving, count);
        }
    }

    /// <inheritdoc/>
    public override void Resize(int columns, int rows)
    {
        var console = Volatile.Read(ref _console);

        if (console == IntPtr.Zero)
        {
            return;
        }

        var window = new Windows.Size
        {
            Columns = (short)Math.Clamp(columns, 1, short.MaxValue),
            Rows = (short)Math.Clamp(rows, 1, short.MaxValue),
        };

        _ = Windows.SizeConsole(console, window);
    }

    /// <inheritdoc/>
    public override int Wait()
    {
        _ending.Wait();

        return _outcome;
    }

    /// <summary>
    /// Asks the command to end, and everything it started with it. They are all in the one job, so the
    /// job is ended rather than the command alone — and the command alone where there is no job.
    /// </summary>
    /// <returns><c>true</c> when there was something to ask.</returns>
    public override bool Interrupt() =>
        (_job != IntPtr.Zero && Windows.EndJob(_job, Stopped)) || Windows.EndProgram(_process, Stopped);

    /// <inheritdoc/>
    public override void Carry(byte[] backlog, int count) => WindowsPassing.Between(this, backlog, count);

    /// <summary>
    /// Closes the near end, which ends anything still reading from the far one. The last hold on the job
    /// goes with it, and with that hold goes whatever the command still had running.
    /// </summary>
    public override void Dispose()
    {
        var process = Interlocked.Exchange(ref _process, IntPtr.Zero);

        if (process == IntPtr.Zero)
        {
            return;
        }

        Dropped(ref _job);
        Hangs();
        Dropped(ref _typing);
        Dropped(ref _reading);

        _ = _ending.Wait(Parting);
        _ = Windows.Close(process);

        _ending.Dispose();
        Marshal.FreeHGlobal(_taking);
        Marshal.FreeHGlobal(_giving);
    }

    /// <summary>Takes what the console has to say, straight, for carrying it through to the real one.</summary>
    /// <param name="buffer">Where to put it.</param>
    /// <param name="count">How much there is room for.</param>
    /// <returns>How much was read, and nought once there is no more coming.</returns>
    internal int Taken(IntPtr buffer, int count)
    {
        var reading = Volatile.Read(ref _reading);

        return reading == IntPtr.Zero ? 0 : Windows.ReadOnce(reading, buffer, count);
    }

    /// <summary>Types at the console, straight, for carrying the real terminal's keyboard through.</summary>
    /// <param name="bytes">What to type.</param>
    /// <param name="count">How much of it.</param>
    /// <returns><c>true</c> when it went.</returns>
    internal bool Fed(IntPtr bytes, int count)
    {
        var typing = Volatile.Read(ref _typing);

        return typing != IntPtr.Zero && Windows.WriteAll(typing, bytes, count);
    }

    /// <summary>
    /// A job for the command and everything it starts, so that stopping it stops all of them. A machine
    /// that will not make one is not refused a terminal: the command runs, and stopping it reaches only it.
    /// </summary>
    /// <returns>The job, or nothing at all when there is none to be had.</returns>
    private static IntPtr Job()
    {
        var job = Windows.MakeJob(IntPtr.Zero, null);

        if (job == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var limits = Marshal.AllocHGlobal(Windows.LimitsRoom);

        try
        {
            for (var at = 0; at < Windows.LimitsRoom; at += 4)
            {
                Marshal.WriteInt32(limits, at, 0);
            }

            Marshal.WriteInt32(limits, Windows.LimitsAt, (int)Windows.EndsWithTheJob);

            if (Windows.TellJob(job, Windows.LongerLimits, limits, Windows.LimitsRoom))
            {
                return job;
            }

            _ = Windows.Close(job);

            return IntPtr.Zero;
        }
        finally
        {
            Marshal.FreeHGlobal(limits);
        }
    }

    /// <summary>
    /// Starts the shell with the command in it, with all three of its streams named as nothing at all,
    /// which is what makes them the console's. It is started stopped and let go once it is in the job.
    /// </summary>
    /// <param name="console">The console it is to have.</param>
    /// <param name="job">The job it is to be in, or nothing at all when there is none.</param>
    /// <param name="command">What was typed.</param>
    /// <param name="folder">The folder to run it in.</param>
    /// <returns>The started program, or nothing at all when nothing started.</returns>
    private static IntPtr Start(IntPtr console, IntPtr job, string command, string folder)
    {
        var room = IntPtr.Zero;

        _ = Windows.OpenAttributes(IntPtr.Zero, 1, 0, ref room);

        var attributes = Marshal.AllocHGlobal(room);
        var words = Marshal.StringToHGlobalUni($"\"{Which()}\" /s /c \"{command}\"");
        var opened = false;

        try
        {
            opened = Windows.OpenAttributes(attributes, 1, 0, ref room);

            if (!opened ||
                !Windows.Attribute(attributes, 0, Windows.TheConsole, console, IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
            {
                return IntPtr.Zero;
            }

            var beginning = default(Windows.Beginning);

            beginning.Opening.Room = Marshal.SizeOf<Windows.Beginning>();
            beginning.Attributes = attributes;

            beginning.Opening.Flags = Windows.OwnStreams;

            var place = Directory.Exists(folder) ? folder : Environment.CurrentDirectory;

            if (!Windows.Start(
                    IntPtr.Zero,
                    words,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    inherited: false,
                    Windows.LongerForm | Windows.WideWords | Windows.StartsStopped,
                    IntPtr.Zero,
                    place,
                    ref beginning,
                    out var started))
            {
                return IntPtr.Zero;
            }

            if (job != IntPtr.Zero)
            {
                _ = Windows.Joins(job, started.Process);
            }

            _ = Windows.Resume(started.Thread);
            _ = Windows.Close(started.Thread);

            return started.Process;
        }
        finally
        {
            if (opened)
            {
                Windows.DropAttributes(attributes);
            }

            Marshal.FreeHGlobal(attributes);
            Marshal.FreeHGlobal(words);
        }
    }

    /// <summary>The shell this machine says it runs command lines through, or the one every one of them has.</summary>
    /// <returns>The path to it.</returns>
    private static string Which() =>
        Environment.GetEnvironmentVariable("COMSPEC") is { Length: > 0 } shell && File.Exists(shell)
            ? shell
            : "cmd.exe";

    /// <summary>
    /// Waits for the command to end and takes the console down after it. A console of the machine's own
    /// outlives the command in it, so without this the reading of it would never hear the end.
    /// </summary>
    private void Watch()
    {
        var process = _process;

        var watching = new Thread(() =>
        {
            _ = Windows.Waited(process, Windows.Forever);

            _outcome = Windows.Ended(process, out var outcome) ? outcome : -1;
            _ended = true;

            _ending.Set();
            Hangs();
        })
        {
            IsBackground = true,
            Name = "arlc-tty",
        };

        watching.Start();
    }

    /// <summary>Lets go of one handle, once however many times it is asked for.</summary>
    /// <param name="handle">The handle, left as nothing at all.</param>
    private static void Dropped(ref IntPtr handle)
    {
        var hold = Interlocked.Exchange(ref handle, IntPtr.Zero);

        if (hold != IntPtr.Zero)
        {
            _ = Windows.Close(hold);
        }
    }

    /// <summary>Takes the console down, once however many times it is asked for.</summary>
    private void Hangs()
    {
        var console = Interlocked.Exchange(ref _console, IntPtr.Zero);

        if (console != IntPtr.Zero)
        {
            Windows.DropConsole(console);
        }
    }
}
