using System;
using System.Runtime.InteropServices;

namespace Arlecchino.Commander.Files.Tty;

/// <summary>
/// The calls a terminal of one's own is made of on Windows. The machine makes a console of its own and
/// hands back two pipes, one to type into it and one to read it by.
/// </summary>
internal static class Windows
{
    private const string Library = "kernel32.dll";

    /// <summary>The keyboard of the real terminal, as this machine numbers the three streams.</summary>
    internal const int Keyboard = -10;

    /// <summary>The screen of the real terminal.</summary>
    internal const int Screen = -11;

    /// <summary>
    /// That the streams a program is to be given are named rather than inherited. Named as nothing at all,
    /// they are the console's own — and left unsaid, a program takes this application's and writes there.
    /// </summary>
    internal const int OwnStreams = 0x100;

    /// <summary>That more is being said about the program than the older, shorter form could carry.</summary>
    internal const uint LongerForm = 0x00080000;

    /// <summary>That its environment is spelled in wide letters.</summary>
    internal const uint WideWords = 0x00000400;

    /// <summary>That it starts stopped, so it can be put in a job before it runs an instruction.</summary>
    internal const uint StartsStopped = 0x00000004;

    /// <summary>Which of the things that can be said about a program is the console it is to have.</summary>
    internal static readonly IntPtr TheConsole = 0x00020016;

    /// <summary>Waiting with no end to the waiting.</summary>
    internal const uint Forever = 0xFFFFFFFF;

    /// <summary>Everything in a job ends when the last hold on the job is let go.</summary>
    internal const uint EndsWithTheJob = 0x2000;

    /// <summary>Room enough for the limits a job is put under, which are read and written whole.</summary>
    internal const int LimitsRoom = 144;

    /// <summary>Where among those limits the flags sit.</summary>
    internal const int LimitsAt = 16;

    /// <summary>Which kind of limits is being set: the longer kind, which is the one with the flags in it.</summary>
    internal const int LongerLimits = 9;

    /// <summary>The flag that turns a key into a signal here rather than into a byte at the far end.</summary>
    internal const uint Signals = 0x0001;

    /// <summary>The flag that gathers what is typed into lines before anyone may read it.</summary>
    internal const uint Lines = 0x0002;

    /// <summary>The flag that writes back whatever is typed.</summary>
    internal const uint Echoes = 0x0004;

    /// <summary>The flag that lets the mouse be heard at all.</summary>
    internal const uint Mouse = 0x0010;

    /// <summary>The flag that gives the mouse to selecting text rather than to whatever is drawing.</summary>
    internal const uint Selecting = 0x0040;

    /// <summary>The flag without which the two above are read as something else entirely.</summary>
    internal const uint TheseFlags = 0x0080;

    /// <summary>The flag that hands keys over as the sequences a terminal sends rather than as records.</summary>
    internal const uint Sequences = 0x0200;

    /// <summary>The flag that has the console read the sequences written to it rather than print them.</summary>
    internal const uint Draws = 0x0004;

    /// <summary>The flag that stops the console adding a return of its own at the end of every line.</summary>
    internal const uint NoReturns = 0x0008;

    /// <summary>The code page everything here is spelled in.</summary>
    internal const uint Utf8 = 65001;

    /// <summary>A key going down, which is the one kind of event worth writing into a keyboard.</summary>
    internal const ushort KeyDown = 1;

    /// <summary>Opens a pipe, one end to write into and one to read out of.</summary>
    /// <param name="reading">The end that is read.</param>
    /// <param name="writing">The end that is written.</param>
    /// <param name="attributes">Who may hold it, which is left as it comes.</param>
    /// <param name="room">How much it holds, which is left as it comes.</param>
    /// <returns><c>true</c> when it worked.</returns>
    [DllImport(Library, EntryPoint = "CreatePipe", SetLastError = true)]
    internal static extern bool MakePipe(out IntPtr reading, out IntPtr writing, IntPtr attributes, int room);

    /// <summary>Lets go of one handle of any kind.</summary>
    /// <param name="handle">The handle.</param>
    /// <returns><c>true</c> when it worked.</returns>
    [DllImport(Library, EntryPoint = "CloseHandle", SetLastError = true)]
    internal static extern bool Close(IntPtr handle);

    /// <summary>Takes what is waiting, and waits when nothing is.</summary>
    /// <param name="handle">What to read.</param>
    /// <param name="buffer">Where to put it.</param>
    /// <param name="count">How much there is room for.</param>
    /// <param name="part">How much was read.</param>
    /// <param name="background">Reading in the background, which nothing here asks for.</param>
    /// <returns><c>false</c> at the end of it and on failure alike.</returns>
    [DllImport(Library, EntryPoint = "ReadFile", SetLastError = true)]
    internal static extern bool Read(IntPtr handle, IntPtr buffer, uint count, out uint part, IntPtr background);

    /// <summary>Writes, and may write less than it was given.</summary>
    /// <param name="handle">What to write to.</param>
    /// <param name="buffer">What to write.</param>
    /// <param name="count">How much of it.</param>
    /// <param name="part">How much went.</param>
    /// <param name="background">Writing in the background, which nothing here asks for.</param>
    /// <returns><c>true</c> when it worked.</returns>
    [DllImport(Library, EntryPoint = "WriteFile", SetLastError = true)]
    internal static extern bool Write(IntPtr handle, IntPtr buffer, uint count, out uint part, IntPtr background);

    /// <summary>Makes a console of the machine's own, spoken to and heard through two pipes.</summary>
    /// <param name="size">How large a window it is to have.</param>
    /// <param name="typing">The end of a pipe it reads what is typed from.</param>
    /// <param name="drawing">The end of a pipe it writes what is drawn to.</param>
    /// <param name="flags">What else to ask of it, which is nothing.</param>
    /// <param name="console">The console.</param>
    /// <returns>Nought when it worked.</returns>
    [DllImport(Library, EntryPoint = "CreatePseudoConsole", SetLastError = true)]
    internal static extern int MakeConsole(Size size, IntPtr typing, IntPtr drawing, uint flags, out IntPtr console);

    /// <summary>Tells the console how large its window is now, which wakes whatever is drawing in it.</summary>
    /// <param name="console">The console.</param>
    /// <param name="size">The window.</param>
    /// <returns>Nought when it worked.</returns>
    [DllImport(Library, EntryPoint = "ResizePseudoConsole", SetLastError = true)]
    internal static extern int SizeConsole(IntPtr console, Size size);

    /// <summary>
    /// Takes the console down, which is what ends the reading of it. Whatever it still had to say is said
    /// first, so it is called from wherever the reading is not, and never from the reader itself.
    /// </summary>
    /// <param name="console">The console.</param>
    [DllImport(Library, EntryPoint = "ClosePseudoConsole", SetLastError = true)]
    internal static extern void DropConsole(IntPtr console);

    /// <summary>Makes room for what is to be said about a program before it starts.</summary>
    /// <param name="attributes">The room, or nothing at all to ask how much is wanted.</param>
    /// <param name="count">How many things will be said.</param>
    /// <param name="flags">Kept for later use, and nought until then.</param>
    /// <param name="room">How much room, asked and answered here.</param>
    /// <returns><c>true</c> when it worked.</returns>
    [DllImport(Library, EntryPoint = "InitializeProcThreadAttributeList", SetLastError = true)]
    internal static extern bool OpenAttributes(IntPtr attributes, int count, int flags, ref IntPtr room);

    /// <summary>Gives back what the attributes took.</summary>
    /// <param name="attributes">The attributes.</param>
    [DllImport(Library, EntryPoint = "DeleteProcThreadAttributeList")]
    internal static extern void DropAttributes(IntPtr attributes);

    /// <summary>Says one thing about the program that is about to start.</summary>
    /// <param name="attributes">The attributes.</param>
    /// <param name="flags">Kept for later use, and nought until then.</param>
    /// <param name="kind">Which of the things that can be said.</param>
    /// <param name="value">What is being said.</param>
    /// <param name="room">How large that is.</param>
    /// <param name="previousValue">What was said before, which nothing here asks after.</param>
    /// <param name="wantedRoom">How much room would have been wanted, which nothing here asks after.</param>
    /// <returns><c>true</c> when it worked.</returns>
    [DllImport(Library, EntryPoint = "UpdateProcThreadAttribute", SetLastError = true)]
    internal static extern bool Attribute(
        IntPtr attributes,
        uint flags,
        IntPtr kind,
        IntPtr value,
        IntPtr room,
        IntPtr previousValue,
        IntPtr wantedRoom);

    /// <summary>Starts a program.</summary>
    /// <param name="program">What to run, or nothing when the words say it.</param>
    /// <param name="words">The command line, in memory the call is free to write over.</param>
    /// <param name="mine">Who may hold the program, which is left as it comes.</param>
    /// <param name="theirs">Who may hold its first thread, which is left as it comes.</param>
    /// <param name="inherited">Whether it takes this application's handles, which it does not.</param>
    /// <param name="flags">How to start it.</param>
    /// <param name="environment">Its environment, or nothing to give it this application's.</param>
    /// <param name="folder">The folder to run it in.</param>
    /// <param name="beginning">Everything else about its beginning.</param>
    /// <param name="started">What was started.</param>
    /// <returns><c>true</c> when it started.</returns>
    [DllImport(Library, EntryPoint = "CreateProcessW", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool Start(
        IntPtr program,
        IntPtr words,
        IntPtr mine,
        IntPtr theirs,
        bool inherited,
        uint flags,
        IntPtr environment,
        string? folder,
        ref Beginning beginning,
        out Started started);

    /// <summary>Lets a thread that was started stopped run.</summary>
    /// <param name="thread">The thread.</param>
    /// <returns>How many holds there were on it, and a negative number on failure.</returns>
    [DllImport(Library, EntryPoint = "ResumeThread", SetLastError = true)]
    internal static extern int Resume(IntPtr thread);

    /// <summary>Waits for something to happen to a handle, or for the time to run out.</summary>
    /// <param name="handle">What to wait on.</param>
    /// <param name="milliseconds">How long to wait.</param>
    /// <returns>Nought when it happened.</returns>
    [DllImport(Library, EntryPoint = "WaitForSingleObject", SetLastError = true)]
    internal static extern uint Waited(IntPtr handle, uint milliseconds);

    /// <summary>Asks how a program ended, and whether it has.</summary>
    /// <param name="process">The program.</param>
    /// <param name="outcome">What it exited with, and a number of the machine's own while it has not.</param>
    /// <returns><c>true</c> when it worked.</returns>
    [DllImport(Library, EntryPoint = "GetExitCodeProcess", SetLastError = true)]
    internal static extern bool Ended(IntPtr process, out int outcome);

    /// <summary>Makes a job, which is a group of programs that live and end together.</summary>
    /// <param name="attributes">Who may hold it, which is left as it comes.</param>
    /// <param name="name">What to call it, which is nothing at all.</param>
    /// <returns>The job, or nothing at all when there is none to be had.</returns>
    [DllImport(Library, EntryPoint = "CreateJobObjectW", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr MakeJob(IntPtr attributes, string? name);

    /// <summary>Puts a job under limits.</summary>
    /// <param name="job">The job.</param>
    /// <param name="kind">Which kind of limits.</param>
    /// <param name="limits">The limits themselves.</param>
    /// <param name="room">How large they are.</param>
    /// <returns><c>true</c> when it worked.</returns>
    [DllImport(Library, EntryPoint = "SetInformationJobObject", SetLastError = true)]
    internal static extern bool TellJob(IntPtr job, int kind, IntPtr limits, int room);

    /// <summary>Puts a program in a job, and everything it starts from then on with it.</summary>
    /// <param name="job">The job.</param>
    /// <param name="process">The program.</param>
    /// <returns><c>true</c> when it worked.</returns>
    [DllImport(Library, EntryPoint = "AssignProcessToJobObject", SetLastError = true)]
    internal static extern bool Joins(IntPtr job, IntPtr process);

    /// <summary>Ends one program, and nothing it started.</summary>
    /// <param name="process">The program.</param>
    /// <param name="outcome">What it is to have exited with.</param>
    /// <returns><c>true</c> when it worked.</returns>
    [DllImport(Library, EntryPoint = "TerminateProcess", SetLastError = true)]
    internal static extern bool EndProgram(IntPtr process, uint outcome);

    /// <summary>Ends everything in a job at once.</summary>
    /// <param name="job">The job.</param>
    /// <param name="outcome">What they are all to have exited with.</param>
    /// <returns><c>true</c> when it worked.</returns>
    [DllImport(Library, EntryPoint = "TerminateJobObject", SetLastError = true)]
    internal static extern bool EndJob(IntPtr job, uint outcome);

    /// <summary>One of the three streams of the real terminal.</summary>
    /// <param name="number">Which of them, as this machine numbers them.</param>
    /// <returns>The stream.</returns>
    [DllImport(Library, EntryPoint = "GetStdHandle", SetLastError = true)]
    internal static extern IntPtr Stream(int number);

    /// <summary>Reads the modes a console is in.</summary>
    /// <param name="handle">The console.</param>
    /// <param name="modes">The modes.</param>
    /// <returns><c>true</c> when it worked, and <c>false</c> where there is no console at all.</returns>
    [DllImport(Library, EntryPoint = "GetConsoleMode", SetLastError = true)]
    internal static extern bool ReadModes(IntPtr handle, out uint modes);

    /// <summary>Puts a console into the modes given.</summary>
    /// <param name="handle">The console.</param>
    /// <param name="modes">The modes.</param>
    /// <returns><c>true</c> when it worked.</returns>
    [DllImport(Library, EntryPoint = "SetConsoleMode", SetLastError = true)]
    internal static extern bool WriteModes(IntPtr handle, uint modes);

    /// <summary>Asks a console about its window and everything around it.</summary>
    /// <param name="handle">The console.</param>
    /// <param name="sheet">What it answered.</param>
    /// <returns><c>true</c> when it worked.</returns>
    [DllImport(Library, EntryPoint = "GetConsoleScreenBufferInfo", SetLastError = true)]
    internal static extern bool Sizing(IntPtr handle, out Sheet sheet);

    /// <summary>The code page what is typed at the real terminal arrives in.</summary>
    /// <returns>The page.</returns>
    [DllImport(Library, EntryPoint = "GetConsoleCP")]
    internal static extern uint KeyboardPage();

    /// <summary>Spells what is typed at the real terminal in a code page.</summary>
    /// <param name="page">The page.</param>
    /// <returns><c>true</c> when it worked.</returns>
    [DllImport(Library, EntryPoint = "SetConsoleCP", SetLastError = true)]
    internal static extern bool SetKeyboardPage(uint page);

    /// <summary>The code page what is written to the real terminal is read in.</summary>
    /// <returns>The page.</returns>
    [DllImport(Library, EntryPoint = "GetConsoleOutputCP")]
    internal static extern uint ScreenPage();

    /// <summary>Spells what is written to the real terminal in a code page.</summary>
    /// <param name="page">The page.</param>
    /// <returns><c>true</c> when it worked.</returns>
    [DllImport(Library, EntryPoint = "SetConsoleOutputCP", SetLastError = true)]
    internal static extern bool SetScreenPage(uint page);

    /// <summary>
    /// Puts an event into the real terminal's keyboard as though it had been typed. It is how a read that
    /// waits for good is let go of: nothing else about a console will interrupt one.
    /// </summary>
    /// <param name="handle">The keyboard.</param>
    /// <param name="press">The event.</param>
    /// <param name="count">How many, which is the one.</param>
    /// <param name="part">How many went.</param>
    /// <returns><c>true</c> when it worked.</returns>
    [DllImport(Library, EntryPoint = "WriteConsoleInputW", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool Wake(IntPtr handle, ref Press press, uint count, out uint part);

    /// <summary>
    /// Writes the whole of a buffer, however many goes it takes. A pipe takes what it has room for and
    /// says how much that was.
    /// </summary>
    /// <param name="handle">What to write to.</param>
    /// <param name="buffer">What to write.</param>
    /// <param name="count">How much of it.</param>
    /// <returns><c>true</c> when all of it went.</returns>
    internal static bool WriteAll(IntPtr handle, IntPtr buffer, int count)
    {
        var at = 0;

        while (at < count)
        {
            if (!Write(handle, buffer + at, (uint)(count - at), out var part) || part == 0)
            {
                return false;
            }

            at += (int)part;
        }

        return true;
    }

    /// <summary>Reads once, and says there is no more coming rather than why there is not.</summary>
    /// <param name="handle">What to read.</param>
    /// <param name="buffer">Where to put it.</param>
    /// <param name="count">How much there is room for.</param>
    /// <returns>How much was read, and nought once there is no more coming.</returns>
    internal static int ReadOnce(IntPtr handle, IntPtr buffer, int count) =>
        Read(handle, buffer, (uint)count, out var part, IntPtr.Zero) ? (int)part : 0;

    /// <summary>Writes, without the caller having to say it wants to hear how much went.</summary>
    /// <param name="handle">What to write to.</param>
    /// <param name="buffer">What to write.</param>
    /// <param name="count">How much of it.</param>
    /// <param name="part">How much went.</param>
    /// <returns><c>true</c> when it worked.</returns>
    private static bool Write(IntPtr handle, IntPtr buffer, uint count, out uint part) =>
        Write(handle, buffer, count, out part, IntPtr.Zero);

    /// <summary>How large a console's window is, and nothing when there is no console to ask.</summary>
    /// <param name="handle">The console.</param>
    /// <returns>The window, or <c>null</c> where the terminal will not say.</returns>
    internal static Size? Measured(IntPtr handle)
    {
        if (!Sizing(handle, out var sheet))
        {
            return null;
        }

        var columns = sheet.Window.Right - sheet.Window.Left + 1;
        var rows = sheet.Window.Bottom - sheet.Window.Top + 1;

        return columns > 0 && rows > 0 ? new Size { Columns = (short)columns, Rows = (short)rows } : null;
    }

    /// <summary>How large a console is, in columns and rows, which is the order the machine takes them in.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Size
    {
        /// <summary>Columns.</summary>
        public short Columns;

        /// <summary>Rows.</summary>
        public short Rows;
    }

    /// <summary>The four sides of the part of a console that is on screen.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Corners
    {
        /// <summary>The leftmost column, counted from nought.</summary>
        public short Left;

        /// <summary>The topmost row.</summary>
        public short Top;

        /// <summary>The rightmost column, which is on the window rather than past it.</summary>
        public short Right;

        /// <summary>The bottom row, likewise.</summary>
        public short Bottom;
    }

    /// <summary>Everything a console will say about itself when asked, of which the window is the part used.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Sheet
    {
        /// <summary>How large the whole sheet is, which is larger than the window when it scrolls.</summary>
        public Size Whole;

        /// <summary>Where the cursor is.</summary>
        public Size Cursor;

        /// <summary>What colors are being written in.</summary>
        public ushort Colors;

        /// <summary>Which part of the sheet is on screen.</summary>
        public Corners Window;

        /// <summary>The largest window this console could have.</summary>
        public Size LargestWindow;
    }

    /// <summary>What is said about a program as it starts, in the older, shorter form.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Opening
    {
        /// <summary>How large this is, which is how the call tells the forms apart.</summary>
        public int Room;

        /// <summary>Kept for later use.</summary>
        public IntPtr ReservedText;

        /// <summary>Which desktop to start it on.</summary>
        public IntPtr Desktop;

        /// <summary>What to call its window.</summary>
        public IntPtr Title;

        /// <summary>Where that window goes.</summary>
        public int X;

        /// <summary>Where that window goes.</summary>
        public int Y;

        /// <summary>How large that window is.</summary>
        public int Width;

        /// <summary>How large that window is.</summary>
        public int Height;

        /// <summary>How large its console is, in columns.</summary>
        public int Columns;

        /// <summary>How large its console is, in rows.</summary>
        public int Rows;

        /// <summary>What colors that console starts in.</summary>
        public int Colors;

        /// <summary>Which of the rest of these have been filled in.</summary>
        public int Flags;

        /// <summary>How its window is to be shown.</summary>
        public short Showing;

        /// <summary>Kept for later use.</summary>
        public short ReservedCount;

        /// <summary>Kept for later use.</summary>
        public IntPtr ReservedBlock;

        /// <summary>The stream it reads from.</summary>
        public IntPtr Typing;

        /// <summary>The stream it writes to.</summary>
        public IntPtr Drawing;

        /// <summary>The stream it complains on.</summary>
        public IntPtr Complaining;
    }

    /// <summary>What is said about a program as it starts, in the form with room for the console.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Beginning
    {
        /// <summary>Everything the older form could say.</summary>
        public Opening Opening;

        /// <summary>Everything it could not, of which the console is one.</summary>
        public IntPtr Attributes;
    }

    /// <summary>What a start hands back.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Started
    {
        /// <summary>The program.</summary>
        public IntPtr Process;

        /// <summary>Its first thread.</summary>
        public IntPtr Thread;

        /// <summary>What the program is numbered.</summary>
        public int Number;

        /// <summary>What the thread is numbered.</summary>
        public int ThreadNumber;
    }

    /// <summary>One event in a console's keyboard, of the one kind that is ever written back into it.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Press
    {
        /// <summary>Which kind of event this is.</summary>
        public ushort Kind;

        /// <summary>Room the machine leaves between the kind and the rest.</summary>
        public ushort Gap;

        /// <summary>Whether the key is going down rather than coming up.</summary>
        public int Down;

        /// <summary>How many times over.</summary>
        public ushort Times;

        /// <summary>Which key it is.</summary>
        public ushort Key;

        /// <summary>Where on the keyboard that key sits.</summary>
        public ushort Place;

        /// <summary>What letter it stands for.</summary>
        public ushort Letter;

        /// <summary>Which of shift, control and the rest were held with it.</summary>
        public uint HeldKeys;
    }
}
