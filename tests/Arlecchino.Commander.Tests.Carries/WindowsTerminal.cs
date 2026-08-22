using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arlecchino.Commander.Tests.Carries;

/// <summary>
/// The console this machine opens a program in. It answers what modes its keyboard and screen are in
/// and which code pages they read and write, and it lets a program write a key into its own keyboard.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsTerminal : RealTerminal
{
    /// <summary>The keyboard of the real terminal, as this machine numbers the three streams.</summary>
    private const int Keyboard = -10;

    /// <summary>The screen of the real terminal.</summary>
    private const int Screen = -11;

    /// <summary>One key going down, which is the one kind of event ever written back into a keyboard.</summary>
    private const ushort Pressing = 1;

    /// <inheritdoc/>
    internal override bool Works =>
        GetConsoleMode(GetStdHandle(Keyboard), out _) && GetConsoleMode(GetStdHandle(Screen), out _);

    /// <summary>
    /// Asked of PowerShell, which is on every machine of this kind, since <c>cmd.exe</c> has no words
    /// for writing an instruction to the terminal or for reading a key without waiting for a line.
    /// </summary>
    internal override string Drawing =>
        "powershell -NoProfile -c \"[Console]::Write([char]27 + '[?1049h'); " +
        $"Write-Host '{Program.Marker}'; $key = [Console]::ReadKey($true); " +
        "[Console]::Write([char]27 + '[?1049l'); " +
        $"if ($key.KeyChar -eq '{Program.TypedLetter}') {{ exit {Program.Answer} }} else {{ exit 8 }}\"";

    /// <inheritdoc/>
    internal override string State()
    {
        _ = GetConsoleMode(GetStdHandle(Keyboard), out var typing);
        _ = GetConsoleMode(GetStdHandle(Screen), out var drawing);

        return $"{typing:X4}/{drawing:X4} pages={GetConsoleCP()}/{GetConsoleOutputCP()}";
    }

    /// <inheritdoc/>
    internal override void Presses(char letter)
    {
        var keyboard = GetStdHandle(Keyboard);
        var press = new Record
        {
            Kind = Pressing,
            Down = 1,
            Times = 1,
            Letter = letter,
        };

        _ = WriteConsoleInputW(keyboard, ref press, 1, out _);

        press.Down = 0;

        _ = WriteConsoleInputW(keyboard, ref press, 1, out _);
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
