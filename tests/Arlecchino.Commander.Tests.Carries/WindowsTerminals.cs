using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

namespace Arlecchino.Commander.Tests.Carries;

/// <summary>
/// The terminals this machine opens: the console any console program starts in, the one shipped with a
/// window of its own, and one people install. Nothing presses a key from here — the try presses it.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsTerminals : Terminals
{
    /// <summary>
    /// The terminal every machine of this kind has: whichever one it opens a console program in. It
    /// needs no one logged in to draw itself, and so it is the one a build server tries.
    /// </summary>
    internal const string Console = "console";

    /// <inheritdoc/>
    internal override string? Missing(string terminal, string shell)
    {
        if (Along($"{shell}.exe") is null)
        {
            return $"{shell} is not on this machine";
        }

        if (Opening(terminal, shell) is null)
        {
            return $"{terminal} is not on this machine";
        }

        return terminal != Console && !Environment.UserInteractive
            ? $"{terminal} draws itself a window, and nobody is logged in here to be shown one"
            : null;
    }

    /// <inheritdoc/>
    internal override bool Opens(string terminal, string shell, string runner, string log)
    {
        if (Opening(terminal, shell) is not { } program)
        {
            return false;
        }

        var started = new ProcessStartInfo { FileName = program, UseShellExecute = terminal == Console };

        foreach (var word in Words(terminal, shell, runner, log))
        {
            started.ArgumentList.Add(word);
        }

        try
        {
            using var running = Process.Start(started);

            return running is not null;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Nothing. The try running in the terminal writes the key into its own keyboard, which is what this
    /// machine allows and what makes a terminal of its own no harder to reach than the console.
    /// </summary>
    /// <param name="terminal">Which terminal, which is not asked.</param>
    /// <param name="letter">The key, which is pressed there.</param>
    internal override void Presses(string terminal, char letter) { }

    /// <inheritdoc/>
    public override void Dispose() { }

    /// <summary>
    /// The words that open one terminal with one shell running one program in it. Each terminal takes
    /// what it is to run after a word of its own, and each shell takes it in a spelling of its own.
    /// </summary>
    /// <param name="terminal">Which terminal.</param>
    /// <param name="shell">Which shell.</param>
    /// <param name="runner">The program to run in it.</param>
    /// <param name="log">Where that program writes.</param>
    /// <returns>The words, for the terminal's own program.</returns>
    private static List<string> Words(string terminal, string shell, string runner, string log)
    {
        var shellWords = shell == "cmd"
            ? new List<string> { "/c", runner, log, terminal, shell }
            : ["-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", "&", $"'{runner}'", $"'{log}'", terminal, shell];

        if (terminal == Console)
        {
            return shellWords;
        }

        var words = terminal == "wt"
            ? new List<string> { "new-tab", "--title", $"arlc-{terminal}-{shell}", "--", shell }
            : ["start", "--", shell];

        words.AddRange(shellWords);

        return words;
    }

    /// <summary>
    /// The program one try is started as: the terminal itself, or the shell where the terminal is
    /// whatever this machine opens a console program in.
    /// </summary>
    /// <param name="terminal">Which terminal.</param>
    /// <param name="shell">Which shell.</param>
    /// <returns>The path to it, or <c>null</c> when it is not on this machine.</returns>
    private static string? Opening(string terminal, string shell) =>
        Along(terminal == Console ? $"{shell}.exe" : $"{terminal}.exe");

    /// <summary>Where a program is, along the path and in the places installers put terminals.</summary>
    /// <param name="program">What it is called.</param>
    /// <returns>The path to it, or <c>null</c> when it is not on this machine.</returns>
    private static string? Along(string program)
    {
        if (Found(program) is { } path)
        {
            return path;
        }

        var files = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var installedPath = Path.Combine(files, "WezTerm", program);

        return File.Exists(installedPath) ? installedPath : null;
    }
}
