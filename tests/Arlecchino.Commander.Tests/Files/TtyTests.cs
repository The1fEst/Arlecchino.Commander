using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Tty;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// A terminal of the application's own making, with a real command at the far end of it, since only a
/// real one can say whether the command believes it. Every fact holds of whichever terminal is made.
/// </summary>
public sealed class TtyTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private static string Folder => Path.GetTempPath();

    /// <summary>Reads everything a command has to say, giving up rather than waiting for good.</summary>
    /// <param name="terminal">The terminal.</param>
    /// <returns>What it printed.</returns>
    private static Task<string> Everything(Tty terminal) => Task.Run(() =>
        {
            var text = new StringBuilder();
            var mouthful = new byte[4096];

            while (true)
            {
                var count = terminal.Read(mouthful);

                if (count <= 0)
                {
                    return text.ToString();
                }

                text.Append(Encoding.UTF8.GetString(mouthful, 0, count));
            }
        })
        .WaitAsync(Patience);

    /// <summary>Waits for the command to end without holding the test there for good.</summary>
    /// <param name="terminal">The terminal.</param>
    /// <returns>What it exited with.</returns>
    private static Task<int> Ended(Tty terminal) => Task.Run(terminal.Wait).WaitAsync(Patience);

    [Fact]
    public async Task WhatTheCommandPrintedComesBack()
    {
        if (!Ttys.Local.Works)
        {
            return;
        }

        using var terminal = Ttys.Local.Open(Asked.Prints("hello"), Folder);

        Assert.NotNull(terminal);
        Assert.Contains("hello", await Everything(terminal), StringComparison.Ordinal);
        Assert.Equal(0, await Ended(terminal));
    }

    /// <summary>
    /// The whole reason for the terminal. On a pipe this command answers that there is none, and every
    /// program that wants the screen believes the same and gives up before it has drawn anything.
    /// </summary>
    [Fact]
    public async Task TheCommandIsAtATerminalAndKnowsIt()
    {
        if (!Ttys.Local.Works)
        {
            return;
        }

        using var terminal = Ttys.Local.Open(Asked.AtATerminal, Folder);

        Assert.NotNull(terminal);
        Assert.Contains("at a terminal", await Everything(terminal), StringComparison.Ordinal);
    }

    /// <summary>The window is the one the real terminal has, since that is the one it will be drawn in.</summary>
    [Fact]
    public async Task TheWindowIsToldToTheCommand()
    {
        if (!Ttys.Local.Works)
        {
            return;
        }

        using var terminal = Ttys.Local.Open(Asked.TheWindow, Folder);

        Assert.NotNull(terminal);

        terminal.Resize(100, 40);

        Assert.Contains("40 100", await Everything(terminal), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheOutcomeIsWhatTheCommandExitedWith()
    {
        if (!Ttys.Local.Works)
        {
            return;
        }

        using var terminal = Ttys.Local.Open(Asked.EndsWith(3), Folder);

        Assert.NotNull(terminal);

        await Everything(terminal);

        Assert.Equal(3, await Ended(terminal));
    }

    /// <summary>
    /// What is typed goes to the command, ended the way this terminal ends a line. Whether it comes back
    /// as well is the terminal's own affair, and the terminal is the one asked.
    /// </summary>
    [Fact]
    public async Task WhatIsTypedReachesTheCommandAndComesBackOnlyWhereItIsEchoed()
    {
        if (!Ttys.Local.Works)
        {
            return;
        }

        using var terminal = Ttys.Local.Open(Asked.Repeats, Folder);

        Assert.NotNull(terminal);

        var typing = Encoding.UTF8.GetBytes("secret" + (char)terminal.Enter);

        Assert.True(terminal.Write(typing, typing.Length));

        var everything = await Everything(terminal);

        Assert.Contains("got [secret]", everything, StringComparison.Ordinal);
        Assert.Equal(terminal.Echoes ? 2 : 1, Times(everything, "secret"));
        Assert.Equal(0, await Ended(terminal));
    }

    /// <summary>Stopping it stops everything it started, which is what the one session is for.</summary>
    [Fact]
    public async Task ItCanBeStopped()
    {
        if (!Ttys.Local.Works)
        {
            return;
        }

        using var terminal = Ttys.Local.Open(Asked.Waits, Folder);

        Assert.NotNull(terminal);
        Assert.True(terminal.Interrupt());
        Assert.True(await Ended(terminal) > 0);
        Assert.False(terminal.IsRunning);
    }

    /// <summary>The folder the panel is looking at is the folder the command runs in.</summary>
    [Fact]
    public async Task ItRunsWhereThePanelIsLooking()
    {
        if (!Ttys.Local.Works)
        {
            return;
        }

        var folder = Directory.CreateTempSubdirectory("arlc-tty").FullName;

        try
        {
            using var terminal = Ttys.Local.Open(Asked.TheFolder, folder);

            Assert.NotNull(terminal);
            Assert.Contains(Path.GetFileName(folder), await Everything(terminal), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>How many times one thing was said inside another.</summary>
    /// <param name="text">What was said.</param>
    /// <param name="word">What to count.</param>
    /// <returns>How many times.</returns>
    private static int Times(string text, string word)
    {
        var times = 0;
        var at = text.IndexOf(word, StringComparison.Ordinal);

        while (at >= 0)
        {
            times++;
            at = text.IndexOf(word, at + word.Length, StringComparison.Ordinal);
        }

        return times;
    }
}
