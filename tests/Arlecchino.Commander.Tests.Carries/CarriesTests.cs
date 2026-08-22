using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Arlecchino.Commander.Tests.Carries;

/// <summary>
/// The handover itself, in the terminals people run this in and under the shells they start it from.
/// Each try opens one, runs <see cref="Program"/> in it, presses a key at the real keyboard from
/// outside, and reads the console back.
/// </summary>
public sealed class CarriesTests
{
    /// <summary>
    /// The terminal every machine of this kind has: whichever one it opens a console program in. It
    /// needs no one logged in to draw itself, and so it is the one a build server tries.
    /// </summary>
    private const string Console = "console";

    /// <summary>How long one try is given, in seconds, since a terminal and a shell both have to start.</summary>
    private const int Patience = 60;

    /// <summary>How often the try's log is looked at while it runs, in milliseconds.</summary>
    private const int Glance = 100;

    private readonly ITestOutputHelper _output;

    /// <summary>Takes somewhere to say what was tried and what was passed over.</summary>
    /// <param name="output">Where that goes, which is the run’s own output.</param>
    public CarriesTests(ITestOutputHelper output) => _output = output;

    /// <summary>Tries the handover in one terminal under one shell.</summary>
    /// <param name="terminal">Which terminal to open.</param>
    /// <param name="shell">Which shell to start in it.</param>
    [Theory]
    [InlineData(Console, "pwsh")]
    [InlineData(Console, "powershell")]
    [InlineData(Console, "cmd")]
    [InlineData("wt", "pwsh")]
    [InlineData("wt", "powershell")]
    [InlineData("wt", "cmd")]
    [InlineData("wezterm", "pwsh")]
    [InlineData("wezterm", "powershell")]
    [InlineData("wezterm", "cmd")]
    public void TheScreenIsCarriedThroughAndGivenBack(string terminal, string shell)
    {
        if (!OperatingSystem.IsWindows())
        {
            Passed("the terminals tried here are made by Windows, which this machine is not");

            return;
        }

        if (Found($"{shell}.exe") is null)
        {
            Passed($"{shell} is not on this machine");

            return;
        }

        if (Opening(terminal, shell) is not { } program)
        {
            Passed($"{terminal} is not on this machine");

            return;
        }

        if (terminal != Console && !Environment.UserInteractive)
        {
            Passed($"{terminal} draws itself a window, and nobody is logged in here to be shown one");

            return;
        }

        if (Runner() is not { } runner)
        {
            Passed("the program a terminal is opened around was not built beside these tests");

            return;
        }

        var log = Path.Combine(Path.GetTempPath(), $"arlecchino-carries-{terminal}-{shell}.log");

        File.Delete(log);

        Assert.True(Opened(program, terminal, shell, runner, log), $"{terminal} would not start");

        var answers = Waited(log);

        Assert.True(answers is not null, $"{terminal} with {shell} never finished the handover");
        Assert.True(Told(answers, "console") == "yes", $"{terminal} gave the program no console of its own");
        Assert.Equal("yes", Told(answers, "claimed"));
        Assert.Equal($"{Program.Answer}", Told(answers, "outcome"));
        Assert.Equal("put back", Told(answers, "modes"));
        Assert.Equal("put back", Told(answers, "pages"));
    }

    /// <summary>
    /// Says why a try was passed over rather than run. This runner has no way to mark a test skipped,
    /// so the reason goes into the run's output instead of behind a green line.
    /// </summary>
    /// <param name="reason">What was missing.</param>
    private void Passed(string reason) => _output.WriteLine($"Passed over: {reason}.");

    /// <summary>
    /// Opens the terminal, with the shell in it, with the one try inside that. A terminal of its own
    /// opens the shell; the machine's own console comes of starting the shell as a person would.
    /// </summary>
    /// <param name="program">The program to start, which is the terminal or the shell.</param>
    /// <param name="terminal">What that terminal is called here.</param>
    /// <param name="shell">The shell to start in it.</param>
    /// <param name="runner">The program that does the one try.</param>
    /// <param name="log">Where that program writes what it found.</param>
    /// <returns><c>true</c> when the terminal started.</returns>
    private static bool Opened(string program, string terminal, string shell, string runner, string log)
    {
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
        Found(terminal == Console ? $"{shell}.exe" : $"{terminal}.exe");

    /// <summary>Waits for the try to write its last line, and gives up rather than waiting for good.</summary>
    /// <param name="log">Where it writes.</param>
    /// <returns>What it wrote, or <c>null</c> when it never finished.</returns>
    private static Dictionary<string, string>? Waited(string log)
    {
        var deadline = Stopwatch.GetTimestamp() + (Stopwatch.Frequency * Patience);

        while (Stopwatch.GetTimestamp() < deadline)
        {
            Thread.Sleep(Glance);

            if (File.Exists(log) && Read(log) is { } answers && answers.ContainsKey("done"))
            {
                return answers;
            }
        }

        return null;
    }

    /// <summary>Reads a log another program may still be writing to.</summary>
    /// <param name="log">The log.</param>
    /// <returns>What it says, or <c>null</c> while it cannot be read.</returns>
    private static Dictionary<string, string>? Read(string log)
    {
        try
        {
            var answers = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var line in File.ReadAllLines(log))
            {
                var at = line.IndexOf('=', StringComparison.Ordinal);

                answers[at < 0 ? line : line[..at]] = at < 0 ? "" : line[(at + 1)..];
            }

            return answers;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string Told(Dictionary<string, string>? answers, string name) =>
        answers is not null && answers.TryGetValue(name, out var what) ? what : "?";

    /// <summary>
    /// The program the terminal is opened around, which is this one: the tests and the try they run are
    /// the same assembly, so what is opened in the terminal is the executable these tests were built as.
    /// </summary>
    /// <returns>The path to it, or <c>null</c> when there is no executable beside the tests.</returns>
    private static string? Runner()
    {
        var name = typeof(Program).Assembly.GetName().Name;
        var beside = Path.Combine(AppContext.BaseDirectory, $"{name}.exe");

        return File.Exists(beside) ? beside : null;
    }

    /// <summary>Where a program is, along the path and in the places installers put terminals.</summary>
    /// <param name="program">What it is called.</param>
    /// <returns>The path to it, or <c>null</c> when it is not on this machine.</returns>
    private static string? Found(string program)
    {
        foreach (var place in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (place.Length > 0 && File.Exists(Path.Combine(place, program)))
            {
                return Path.Combine(place, program);
            }
        }

        var files = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var installedPath = Path.Combine(files, "WezTerm", program);

        return File.Exists(installedPath) ? installedPath : null;
    }
}
