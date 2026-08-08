using System;
using Arlecchino.Commander.Tests.Support;
using Xunit;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
///     The command line under the panels: what typing puts on it, what it says when nobody is typing, and
///     what becomes of text the terminal hands over in a block rather than key by key.
/// </summary>
public sealed class CommanderLineTests : IDisposable
{
    private readonly ScreenApp _app = Started.Showing();

    public void Dispose()
    {
        _app.Dispose();
    }

    [Fact]
    public void WhatIsTypedOnThePromptIsShownThere()
    {
        _app.Frame();
        _app.Type(":echo hello");

        Assert.Contains("echo hello", _app.Frame(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     The row says whether it has the keyboard. It used to take letters whenever they were typed, so
    ///     there was nothing to say; now that it is asked for, a row that looks the same either way leaves
    ///     the keyboard somewhere the eye cannot find it. Asleep it names the key that wakes it instead.
    /// </summary>
    [Fact]
    public void ThePromptSaysWhetherItHasTheKeyboard()
    {
        var asleep = _app.FrameLines()[_app.CommandLineRow()];

        Assert.Contains("type a command here", asleep, StringComparison.Ordinal);

        _app.Type(":");

        var awake = _app.FrameLines()[_app.CommandLineRow()];

        Assert.DoesNotContain("type a command here", awake, StringComparison.Ordinal);
        Assert.Contains("everything the commands printed", awake, StringComparison.Ordinal);
    }

    [Fact]
    public void RubbingOutTakesTheLastLetterOffThePrompt()
    {
        _app.Frame();
        _app.Type(":echo hello");
        _app.Press(ConsoleKey.Backspace);

        var screen = _app.Frame();

        Assert.Contains("echo hell", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("echo hello", screen, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A paste is not a run of key presses — the terminal hands it over as a block of its own — so a
    ///     screen that only answers keys loses it. What was pasted goes on the line at the cursor, the same
    ///     place typing would have put it.
    /// </summary>
    [Fact]
    public void PastedTextLandsOnThePrompt()
    {
        _app.Frame();
        _app.Type(":echo ");
        _app.ReadFromTerminal("\e[200~hello\e[201~");

        Assert.Contains("echo hello", _app.Frame(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     A paste while the panel has the keyboard wakes the line and lands there. There is nowhere else on
    ///     this screen for text to go, and a paste that vanishes is the one thing worse than a paste that
    ///     asked first.
    /// </summary>
    [Fact]
    public void APasteWakesThePromptAndLandsOnIt()
    {
        _app.Frame();
        _app.ReadFromTerminal("\e[200~git status\e[201~");

        var line = _app.FrameLines()[_app.CommandLineRow()];

        Assert.Contains("git status", line, StringComparison.Ordinal);
        Assert.DoesNotContain("type a command here", line, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Only the first line of a paste reaches the row, and none of it is run. A clipboard carrying a
    ///     newline would otherwise press Enter on a command nobody has read yet.
    /// </summary>
    [Fact]
    public void OnlyTheFirstLineOfAPasteReachesThePrompt()
    {
        _app.Frame();
        _app.ReadFromTerminal("\e[200~echo one\r\nrm -rf two\e[201~");

        var line = _app.FrameLines()[_app.CommandLineRow()];

        Assert.Contains("echo one", line, StringComparison.Ordinal);
        Assert.DoesNotContain("rm -rf two", _app.Frame(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     While the search that runs as you type has the keyboard, a paste is part of what is being spelled
    ///     rather than the start of a command.
    /// </summary>
    [Fact]
    public void PastingIntoTheSearchSpellsTheNameOut()
    {
        _app.Settled();
        _app.Press(ConsoleKey.Oem2);
        _app.ReadFromTerminal("\e[200~bet\e[201~");

        var line = _app.FrameLines()[_app.CommandLineRow()];

        Assert.Contains("jump to  bet", _app.Frame(), StringComparison.Ordinal);
        Assert.Contains("type a command here", line, StringComparison.Ordinal);
    }
}
