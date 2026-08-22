using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Arlecchino.Commander.Files.Tty;

/// <summary>
/// The calls a terminal of one's own is made of that are reached alike on every POSIX machine. What is
/// not — the numbers they take, and the one call a machine may spell otherwise — is <see cref="PosixTtys"/>.
/// </summary>
internal static class Posix
{
    /// <summary>Where all of these live, on every machine that has them.</summary>
    internal const string Library = "libc";

    /// <summary>Opened for both reading and writing, which every end of a terminal is.</summary>
    internal const int ReadWrite = 2;

    /// <summary>Change the modes of a terminal at once rather than once what is queued has gone.</summary>
    internal const int Instant = 0;

    /// <summary>There is something to read.</summary>
    internal const short Readable = 1;

    /// <summary>The other end has gone, in either of the two ways a wait reports it.</summary>
    internal const short Hangup = 0x18;

    /// <summary>The call was cut short by a signal and is worth making again.</summary>
    internal const int Interruption = 4;

    /// <summary>Asks a program to end.</summary>
    internal const int Terminate = 15;

    /// <summary>Room enough for the modes of any terminal, which are read and written whole.</summary>
    internal const int ModesRoom = 128;

    /// <summary>The flag that writes back whatever is typed.</summary>
    internal const uint Echoes = 8;

    /// <summary>Opens the near end of a fresh pair.</summary>
    /// <param name="flags">How to open it.</param>
    /// <returns>The near end, or a negative number when there is none to be had.</returns>
    [DllImport(Library, EntryPoint = "posix_openpt", SetLastError = true)]
    internal static extern int OpenTerminal(int flags);

    /// <summary>Makes the far end of the pair belong to whoever opened the near one.</summary>
    /// <param name="master">The near end.</param>
    /// <returns>Nought when it worked.</returns>
    [DllImport(Library, EntryPoint = "grantpt", SetLastError = true)]
    internal static extern int Grant(int master);

    /// <summary>Leaves the far end open to be opened, which until now it was not.</summary>
    /// <param name="master">The near end.</param>
    /// <returns>Nought when it worked.</returns>
    [DllImport(Library, EntryPoint = "unlockpt", SetLastError = true)]
    internal static extern int Unlock(int master);

    /// <summary>What the far end of the pair is called, as a path a program can open.</summary>
    /// <param name="master">The near end.</param>
    /// <returns>The name, in memory belonging to the library.</returns>
    [DllImport(Library, EntryPoint = "ptsname", SetLastError = true)]
    internal static extern IntPtr FarEnd(int master);

    /// <summary>Takes what is waiting, and waits when nothing is.</summary>
    /// <param name="handle">What to read.</param>
    /// <param name="buffer">Where to put it.</param>
    /// <param name="count">How much there is room for.</param>
    /// <returns>How much was read, nought at the end of it, a negative number on failure.</returns>
    [DllImport(Library, EntryPoint = "read", SetLastError = true)]
    internal static extern nint Read(int handle, IntPtr buffer, nuint count);

    /// <summary>Writes, and may write less than it was given.</summary>
    /// <param name="handle">What to write to.</param>
    /// <param name="buffer">What to write.</param>
    /// <param name="count">How much of it.</param>
    /// <returns>How much went, or a negative number on failure.</returns>
    [DllImport(Library, EntryPoint = "write", SetLastError = true)]
    internal static extern nint Write(int handle, IntPtr buffer, nuint count);

    /// <summary>Closes one end.</summary>
    /// <param name="handle">The end.</param>
    /// <returns>Nought when it worked.</returns>
    [DllImport(Library, EntryPoint = "close", SetLastError = true)]
    internal static extern int Close(int handle);

    /// <summary>Waits until one of several things has something to say, or until the time runs out.</summary>
    /// <param name="watches">What to watch, answered in place.</param>
    /// <param name="count">How many of them.</param>
    /// <param name="milliseconds">How long to wait.</param>
    /// <returns>How many have something to say, nought when the time ran out.</returns>
    [DllImport(Library, EntryPoint = "poll", SetLastError = true)]
    internal static extern int Poll([In] [Out] Watch[] watches, uint count, int milliseconds);

    /// <summary>Reads the modes a terminal is in.</summary>
    /// <param name="handle">The terminal.</param>
    /// <param name="modes">Where to put them.</param>
    /// <returns>Nought when it worked.</returns>
    [DllImport(Library, EntryPoint = "tcgetattr", SetLastError = true)]
    internal static extern int ReadModes(int handle, IntPtr modes);

    /// <summary>Puts a terminal into the modes given.</summary>
    /// <param name="handle">The terminal.</param>
    /// <param name="instant">When the change takes hold.</param>
    /// <param name="modes">The modes.</param>
    /// <returns>Nought when it worked.</returns>
    [DllImport(Library, EntryPoint = "tcsetattr", SetLastError = true)]
    internal static extern int WriteModes(int handle, int instant, IntPtr modes);

    /// <summary>
    /// Turns modes into the raw ones: nothing echoed, nothing gathered into lines, no key turned into a
    /// signal. Every terminal a program draws in is put into them.
    /// </summary>
    /// <param name="modes">The modes, changed in place.</param>
    [DllImport(Library, EntryPoint = "cfmakeraw")]
    internal static extern void MakeRaw(IntPtr modes);

    /// <summary>Waits for a started program to end, or asks whether it has.</summary>
    /// <param name="child">The program.</param>
    /// <param name="status">How it ended.</param>
    /// <param name="options">One to ask without waiting, nought to wait.</param>
    /// <returns>The program once it has ended, nought when it has not.</returns>
    [DllImport(Library, EntryPoint = "waitpid", SetLastError = true)]
    internal static extern int Waited(int child, out int status, int options);

    /// <summary>Sends a signal to a program, or to a whole group of them when the number is negative.</summary>
    /// <param name="child">The program or the group.</param>
    /// <param name="signal">The signal.</param>
    /// <returns>Nought when it worked.</returns>
    [DllImport(Library, EntryPoint = "kill", SetLastError = true)]
    internal static extern int Signal(int child, int signal);

    /// <summary>Starts a program without the fork this runtime cannot safely make.</summary>
    /// <param name="child">The program that was started.</param>
    /// <param name="path">What to run.</param>
    /// <param name="actions">What to do with the three streams before it runs.</param>
    /// <param name="attributes">What else to change about it before it runs.</param>
    /// <param name="argv">The arguments, ending in nothing.</param>
    /// <param name="envp">The environment, ending in nothing.</param>
    /// <returns>Nought when it started, and the reason itself when it did not.</returns>
    [DllImport(Library, EntryPoint = "posix_spawn", SetLastError = true)]
    internal static extern int Spawn(
        out int child,
        IntPtr path,
        IntPtr actions,
        IntPtr attributes,
        IntPtr argv,
        IntPtr envp);

    /// <summary>Makes room for what to do with the streams.</summary>
    /// <param name="actions">The room.</param>
    /// <returns>Nought when it worked.</returns>
    [DllImport(Library, EntryPoint = "posix_spawn_file_actions_init", SetLastError = true)]
    internal static extern int OpenActions(IntPtr actions);

    /// <summary>Gives back what the actions took.</summary>
    /// <param name="actions">The actions.</param>
    /// <returns>Nought when it worked.</returns>
    [DllImport(Library, EntryPoint = "posix_spawn_file_actions_destroy", SetLastError = true)]
    internal static extern int DropActions(IntPtr actions);

    /// <summary>Has the program open something as one of its streams.</summary>
    /// <param name="actions">The actions.</param>
    /// <param name="handle">Which stream.</param>
    /// <param name="path">What to open.</param>
    /// <param name="flags">How to open it.</param>
    /// <param name="mode">What to make it, when opening makes it.</param>
    /// <returns>Nought when it worked.</returns>
    [DllImport(Library, EntryPoint = "posix_spawn_file_actions_addopen", SetLastError = true)]
    internal static extern int ActionOpens(IntPtr actions, int handle, IntPtr path, int flags, uint mode);

    /// <summary>Has the program point one of its streams at another.</summary>
    /// <param name="actions">The actions.</param>
    /// <param name="handle">The stream that is already open.</param>
    /// <param name="asHandle">The stream to point at it.</param>
    /// <returns>Nought when it worked.</returns>
    [DllImport(Library, EntryPoint = "posix_spawn_file_actions_adddup2", SetLastError = true)]
    internal static extern int ActionPoints(IntPtr actions, int handle, int asHandle);

    /// <summary>Makes room for what else to change about the program.</summary>
    /// <param name="attributes">The room.</param>
    /// <returns>Nought when it worked.</returns>
    [DllImport(Library, EntryPoint = "posix_spawnattr_init", SetLastError = true)]
    internal static extern int OpenAttributes(IntPtr attributes);

    /// <summary>Gives back what the attributes took.</summary>
    /// <param name="attributes">The attributes.</param>
    /// <returns>Nought when it worked.</returns>
    [DllImport(Library, EntryPoint = "posix_spawnattr_destroy", SetLastError = true)]
    internal static extern int DropAttributes(IntPtr attributes);

    /// <summary>Sets which of the changes are asked for.</summary>
    /// <param name="attributes">The attributes.</param>
    /// <param name="flags">The changes.</param>
    /// <returns>Nought when it worked.</returns>
    [DllImport(Library, EntryPoint = "posix_spawnattr_setflags", SetLastError = true)]
    internal static extern int Attribute(IntPtr attributes, short flags);

    /// <summary>
    /// Writes the whole of a buffer, however many goes it takes. A terminal takes what it has room for and
    /// says how much that was.
    /// </summary>
    /// <param name="handle">What to write to.</param>
    /// <param name="buffer">What to write.</param>
    /// <param name="count">How much of it.</param>
    /// <returns><c>true</c> when all of it went.</returns>
    internal static bool WriteAll(int handle, IntPtr buffer, int count)
    {
        var at = 0;

        while (at < count)
        {
            var part = Write(handle, buffer + at, (nuint)(count - at));

            if (part > 0)
            {
                at += (int)part;

                continue;
            }

            if (Marshal.GetLastPInvokeError() != Interruption)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads once, trying again when a signal cut the wait short.</summary>
    /// <param name="handle">What to read.</param>
    /// <param name="buffer">Where to put it.</param>
    /// <param name="count">How much there is room for.</param>
    /// <returns>How much was read, and nought or less when there is no more coming.</returns>
    internal static int ReadOnce(int handle, IntPtr buffer, int count)
    {
        while (true)
        {
            var part = (int)Read(handle, buffer, (nuint)count);

            if (part >= 0 || Marshal.GetLastPInvokeError() != Interruption)
            {
                return part;
            }
        }
    }

    /// <summary>The exit status of a program, read the way a shell reads it.</summary>
    /// <param name="status">What waiting for it answered.</param>
    /// <returns>What it exited with, or the signal that ended it counted from a hundred and twenty-eight.</returns>
    internal static int Outcome(int status) =>
        (status & 0x7F) == 0 ? (status >> 8) & 0xFF : 128 + (status & 0x7F);

    /// <summary>Puts a string in memory of its own, as a C library wants it.</summary>
    /// <param name="text">The string.</param>
    /// <returns>Where it went, to be given back later.</returns>
    internal static IntPtr Held(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var block = Marshal.AllocHGlobal(bytes.Length + 1);

        Marshal.Copy(bytes, 0, block, bytes.Length);
        Marshal.WriteByte(block, bytes.Length, 0);

        return block;
    }

    /// <summary>
    /// Puts a list of strings in memory of its own, ending in nothing at all. That ending is what tells the
    /// library where the list stops, and the runtime's own marshalling leaves it off.
    /// </summary>
    /// <param name="texts">The strings.</param>
    /// <param name="blocks">Everything allocated, to be given back later.</param>
    /// <returns>The list itself.</returns>
    internal static IntPtr HeldList(IReadOnlyList<string> texts, List<IntPtr> blocks)
    {
        var list = Marshal.AllocHGlobal(IntPtr.Size * (texts.Count + 1));

        blocks.Add(list);

        for (var at = 0; at < texts.Count; at++)
        {
            var text = Held(texts[at]);

            blocks.Add(text);
            Marshal.WriteIntPtr(list, at * IntPtr.Size, text);
        }

        Marshal.WriteIntPtr(list, texts.Count * IntPtr.Size, IntPtr.Zero);

        return list;
    }

    /// <summary>Gives back everything that was held for one call.</summary>
    /// <param name="blocks">What was allocated.</param>
    internal static void Free(List<IntPtr> blocks)
    {
        foreach (var block in blocks)
        {
            Marshal.FreeHGlobal(block);
        }

        blocks.Clear();
    }

    /// <summary>How large a terminal's window is, in rows and columns and in nothing else.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Window
    {
        /// <summary>Rows.</summary>
        public ushort Rows;

        /// <summary>Columns.</summary>
        public ushort Columns;

        /// <summary>Width in pixels, which nothing here asks after.</summary>
        public ushort Width;

        /// <summary>Height in pixels, which nothing here asks after.</summary>
        public ushort Height;
    }

    /// <summary>One thing being waited on, and what it turned out to have to say.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Watch
    {
        /// <summary>What is being watched.</summary>
        public int Handle;

        /// <summary>What is worth being woken for.</summary>
        public short Events;

        /// <summary>What happened.</summary>
        public short Report;
    }
}
