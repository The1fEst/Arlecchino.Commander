using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Ssh;
using Arlecchino.Commander.Files.Tty;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// A command run at a terminal of the application's own making. What it printed goes to the roll, what it
/// stopped to ask is put to the user, and the screen is lent the moment it asks and not before.
/// </summary>
public sealed class TerminalRunTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private readonly List<string> _lines = [];
    private readonly List<string> _questions = [];

    private int _lendings;

    private static string Folder => Path.GetTempPath();

    /// <summary>
    /// Runs a command to its end. The screen is not really lent: carrying the terminal through is left
    /// undone, and the command is stopped instead, since a test has no terminal of its own to lend.
    /// </summary>
    /// <param name="command">What to run.</param>
    /// <param name="answer">What to type at it when it asks, or nothing.</param>
    /// <returns>The run, once it has ended.</returns>
    private async Task<TerminalRun> Run(string command, string? answer = null)
    {
        var run = new TerminalRun(command, Folder);

        var talk = new ShellTalk(
            line => _lines.Add(line),
            question =>
            {
                _questions.Add(question);

                if (answer is not null)
                {
                    run.Say(answer);
                }
            },
            _ =>
            {
                Interlocked.Increment(ref _lendings);
                run.Interrupt();

                return Task.CompletedTask;
            });

        await run.ReadAsync(talk, CancellationToken.None).WaitAsync(Patience);

        return run;
    }

    [Fact]
    public async Task WhatACommandPrintsIsKeptAndTheScreenIsLeftAlone()
    {
        if (!TerminalRun.Works)
        {
            return;
        }

        using var run = await Run("printf 'plain\\n'");

        Assert.Contains("plain", _lines);
        Assert.Contains("[exit 0]", _lines);
        Assert.Equal(0, _lendings);
    }

    /// <summary>
    /// The one that matters. Nothing about the command says beforehand that it wants the screen — it is
    /// asked for in what it writes, and the writing is what is read.
    /// </summary>
    [Fact]
    public async Task TheScreenGoesToACommandThatAsksForIt()
    {
        if (!TerminalRun.Works)
        {
            return;
        }

        using var run = await Run("printf 'before\\n'; printf '\\033[?1049h'; sleep 30");

        Assert.Equal(1, _lendings);
        Assert.Contains("before", _lines);
    }

    /// <summary>Color and the rest of what a command writes to a terminal is not asking for the screen.</summary>
    [Fact]
    public async Task ColorIsNotAskingForTheScreen()
    {
        if (!TerminalRun.Works)
        {
            return;
        }

        using var run = await Run("printf '\\033[32mgreen\\033[0m\\n'");

        Assert.Equal(0, _lendings);
        Assert.Contains("green", _lines);
    }

    /// <summary>
    /// A command that stops mid-line is waiting to be told something. At a terminal it is answered by
    /// typing at it, which is what the dialog's answer does.
    /// </summary>
    [Fact]
    public async Task AQuestionIsPutAndWhatIsAnsweredReachesTheCommand()
    {
        if (!TerminalRun.Works)
        {
            return;
        }

        using var run = await Run("printf 'password:'; read given; printf 'got %s\\n' \"$given\"", "opened");

        Assert.Contains("password:", _questions);
        Assert.Contains("got opened", _lines);
        Assert.Equal(0, _lendings);
    }

    [Fact]
    public async Task HowItEndedIsTheLastThingSaid()
    {
        if (!TerminalRun.Works)
        {
            return;
        }

        using var run = await Run("exit 3");

        Assert.Equal("[exit 3]", _lines[^1]);
    }
}
