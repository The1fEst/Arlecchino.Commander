using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Tty;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// A terminal of the application's own making, with a real command at the far end of it. Only the machine
/// can say whether the command believes it is at a terminal, so a real one is started here.
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
    }).WaitAsync(Patience);

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

        using var terminal = Ttys.Local.Open("printf 'hello\\n'", Folder);

        Assert.NotNull(terminal);
        Assert.Contains("hello", await Everything(terminal), StringComparison.Ordinal);
        Assert.Equal(0, await Ended(terminal));
    }

    /// <summary>
    /// The whole reason for the pair. On a pipe this command answers that there is no terminal, and every
    /// program that wants the screen believes the same and gives up before it has drawn anything.
    /// </summary>
    [Fact]
    public async Task TheCommandIsAtATerminalAndKnowsIt()
    {
        if (!Ttys.Local.Works)
        {
            return;
        }

        using var terminal = Ttys.Local.Open("test -t 0 && test -t 1 && echo at a terminal", Folder);

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

        using var terminal = Ttys.Local.Open("stty size", Folder);

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

        using var terminal = Ttys.Local.Open("exit 3", Folder);

        Assert.NotNull(terminal);

        await Everything(terminal);

        Assert.Equal(3, await Ended(terminal));
    }

    /// <summary>
    /// What is typed goes to the command and is not written back, since what is typed at a command that
    /// stopped to ask is a password more often than not.
    /// </summary>
    [Fact]
    public async Task WhatIsTypedReachesTheCommandAndIsNotEchoed()
    {
        if (!Ttys.Local.Works)
        {
            return;
        }

        using var terminal = Ttys.Local.Open("cat", Folder);

        Assert.NotNull(terminal);
        Assert.True(terminal.Write(Encoding.UTF8.GetBytes("secret\n"), 7));

        var mouthful = new byte[4096];
        var count = await Task.Run(() => terminal.Read(mouthful)).WaitAsync(Patience);

        Assert.Equal("secret\r\n", Encoding.UTF8.GetString(mouthful, 0, count));

        Assert.True(terminal.Write([0x04], 1));
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

        using var terminal = Ttys.Local.Open("sleep 30", Folder);

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
            using var terminal = Ttys.Local.Open("pwd", folder);

            Assert.NotNull(terminal);
            Assert.Contains(Path.GetFileName(folder), await Everything(terminal), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
