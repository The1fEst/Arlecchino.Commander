using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Arlecchino.Commander.Tests.Carries;

/// <summary>
/// The handover itself, in the terminals people run this in and under the shells they start it from.
/// Each try opens one, runs <see cref="Program"/> in it, presses a key, and reads what it found.
/// </summary>
public sealed class CarriesTests
{
    /// <summary>How long one try is given, in seconds, since a terminal and a shell both have to start.</summary>
    private const int TimeoutSeconds = 60;

    /// <summary>How often the try's log is looked at while it runs, in milliseconds.</summary>
    private const int PollIntervalMilliseconds = 100;

    /// <summary>How long one press is given to reach the program on the screen, in seconds.</summary>
    private const int RetryIntervalSeconds = 2;

    private readonly ITestOutputHelper _output;

    /// <summary>Takes somewhere to say what was tried and what was passed over.</summary>
    /// <param name="output">Where that goes, which is the run’s own output.</param>
    public CarriesTests(ITestOutputHelper output) => _output = output;

    /// <summary>Tries the handover in one terminal under one shell.</summary>
    /// <param name="terminal">Which terminal to open.</param>
    /// <param name="shell">Which shell to start in it.</param>
    [Theory]
    [InlineData("console", "pwsh")]
    [InlineData("console", "powershell")]
    [InlineData("console", "cmd")]
    [InlineData("wt", "pwsh")]
    [InlineData("wt", "powershell")]
    [InlineData("wt", "cmd")]
    [InlineData("wezterm", "pwsh")]
    [InlineData("wezterm", "powershell")]
    [InlineData("wezterm", "cmd")]
    [InlineData("headless", "zsh")]
    [InlineData("headless", "bash")]
    [InlineData("headless", "fish")]
    [InlineData("headless", "sh")]
    [InlineData("Terminal", "zsh")]
    [InlineData("Terminal", "bash")]
    [InlineData("Terminal", "fish")]
    [InlineData("Terminal", "sh")]
    [InlineData("kitty", "zsh")]
    [InlineData("kitty", "bash")]
    [InlineData("kitty", "fish")]
    [InlineData("kitty", "sh")]
    public void TheScreenIsCarriedThroughAndGivenBack(string terminal, string shell)
    {
        using var terminals = Terminals.OfThisMachine();

        if (terminals.Missing(terminal, shell) is { } missing)
        {
            Passed(missing);

            return;
        }

        if (Runner() is not { } runner)
        {
            Passed("the program a terminal is opened around was not built beside these tests");

            return;
        }

        var log = Path.Combine(Path.GetTempPath(), $"arlecchino-carries-{terminal}-{shell}.log");

        File.Delete(log);

        Assert.True(terminals.Opens(terminal, shell, runner, log), $"{terminal} would not start");
        Assert.True(Waited(log, "claimed", TimeoutSeconds) is not null, $"{terminal} with {shell} never gave the screen away");

        var answers = Pressed(terminals, terminal, log);

        Assert.True(answers is not null, $"{terminal} with {shell} never finished the handover");
        Assert.Equal("yes", Told(answers, "console"));
        Assert.Equal("yes", Told(answers, "claimed"));
        Assert.Equal($"{Program.Answer}", Told(answers, "outcome"));
        Assert.Equal("put back", Told(answers, "modes"));
    }

    /// <summary>
    /// Says why a try was passed over rather than run. This runner has no way to mark a test skipped,
    /// so the reason goes into the run's output instead of behind a green line.
    /// </summary>
    /// <param name="reason">What was missing.</param>
    private void Passed(string reason) => _output.WriteLine($"Passed over: {reason}.");

    /// <summary>
    /// Presses the key and waits for the try to finish, pressing again for as long as it has not. A key
    /// typed into a line the terminal has yet to end never reaches the program on the screen.
    /// </summary>
    /// <param name="terminals">The terminals of this machine, which is what presses.</param>
    /// <param name="terminal">The one that was opened.</param>
    /// <param name="log">Where the try writes.</param>
    /// <returns>What the try wrote, or <c>null</c> when it never finished.</returns>
    private static Dictionary<string, string>? Pressed(Terminals terminals, string terminal, string log)
    {
        var deadline = Stopwatch.GetTimestamp() + (Stopwatch.Frequency * TimeoutSeconds);

        while (Stopwatch.GetTimestamp() < deadline)
        {
            terminals.Presses(terminal, Program.TypedLetter);

            if (Waited(log, "done", RetryIntervalSeconds) is { } answers)
            {
                return answers;
            }
        }

        return null;
    }

    /// <summary>Waits for the try to write a line down, and gives up rather than waiting for good.</summary>
    /// <param name="log">Where it writes.</param>
    /// <param name="name">The line to wait for.</param>
    /// <param name="patience">How long to wait for it, in seconds.</param>
    /// <returns>What it has written, or <c>null</c> when that line never came.</returns>
    private static Dictionary<string, string>? Waited(string log, string name, int patience)
    {
        var deadline = Stopwatch.GetTimestamp() + (Stopwatch.Frequency * patience);

        while (Stopwatch.GetTimestamp() < deadline)
        {
            if (File.Exists(log) && Read(log) is { } answers && answers.ContainsKey(name))
            {
                return answers;
            }

            Thread.Sleep(PollIntervalMilliseconds);
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
        var beside = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? $"{name}.exe" : $"{name}");

        return File.Exists(beside) ? beside : null;
    }
}
