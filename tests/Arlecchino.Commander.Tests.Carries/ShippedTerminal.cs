using System;
using System.IO;

namespace Arlecchino.Commander.Tests.Carries;

/// <summary>
/// The terminal this machine ships with, told what to do in the tongue the machine talks to its
/// programs in. The window opened here is found again by the pair of ends behind it.
/// </summary>
internal sealed class ShippedTerminal : OpenedTerminal
{
    /// <summary>What it is called, both in the try's log and among the windows.</summary>
    internal const string Name = "Terminal";

    /// <summary>Where the machine keeps it.</summary>
    private const string Place = "/System/Applications/Utilities/Terminal.app";

    /// <summary>Where a machine that has not moved it there yet keeps it.</summary>
    private const string OldPlace = "/Applications/Utilities/Terminal.app";

    private string _tty = "";

    /// <inheritdoc/>
    internal override string? Missing()
    {
        if (!Directory.Exists(Place) && !Directory.Exists(OldPlace))
        {
            return $"{Name} is not on this machine";
        }

        return Processes.Answered("osascript", "-e", Scripted("return name")).Length == 0
            ? $"this machine does not let one program tell {Name} what to do"
            : null;
    }

    /// <inheritdoc/>
    internal override bool Opens(string shell, string command)
    {
        var opening = $"do script \"exec {shell} -c {Escaped(Processes.Quoted(command))}\"";

        _tty = Processes.Answered("osascript", "-e", Scripted($"activate\nreturn tty of ({opening})"));

        return _tty.Length > 0;
    }

    /// <inheritdoc/>
    internal override void Presses(char letter)
    {
        if (_tty.Length > 0)
        {
            _ = Processes.Answered("osascript", "-e", Scripted(Behind(_tty, $"do script \"{letter}\" in one")));
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_tty.Length > 0)
        {
            _ = Processes.Answered("osascript", "-e", Scripted(Behind(_tty, "close pane")));
            _tty = "";
        }
    }

    /// <summary>Wraps what the terminal is to be told, in the tongue the machine talks to its programs in.</summary>
    /// <param name="script">What to tell it.</param>
    /// <returns>The whole of what the program that speaks that tongue is handed.</returns>
    private static string Scripted(string script) => $"tell application \"{Name}\"\n{script}\nend tell";

    /// <summary>
    /// Wraps what is to be done to the one window opened here, which is found by the pair of ends behind
    /// it rather than by its place among the person's other windows.
    /// </summary>
    /// <param name="tty">What the pair behind that window is called.</param>
    /// <param name="script">What to do once it is found, where <c>one</c> is the window's tab.</param>
    /// <returns>What to tell the terminal.</returns>
    private static string Behind(string tty, string script) =>
        $"repeat with pane in windows\nrepeat with one in tabs of pane\nif tty of one is \"{tty}\" then\n" +
        $"{script}\nreturn \"\"\nend if\nend repeat\nend repeat\nreturn \"\"";

    /// <summary>
    /// Makes a word safe to stand inside a line of that tongue, where a backslash and a quotation mark
    /// both mean something to whoever reads it.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns>The word as it is written there.</returns>
    private static string Escaped(string word) => word
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal);
}
