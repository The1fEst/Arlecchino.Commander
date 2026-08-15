using System;
using System.Linq;
using Arlecchino.Commander.Tests.Support;
using Xunit;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
///     The command line under the panels: what typing puts on it, what it says while it is closed, and what
///     becomes of text the terminal hands over in a block rather than key by key.
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
    ///     The row says whether it has the keyboard. Closed, it names the key that opens it instead.
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
    ///     A paste is not a run of key presses: the terminal hands it over as a block of its own. What was
    ///     pasted goes on the line at the cursor, where typing would have put it.
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
    ///     A paste while the panel has the keyboard opens the line and lands there, since there is nowhere
    ///     else on this screen for text to go.
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
    ///     newline would otherwise press Enter on a command that has not been read yet.
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

    /// <summary>Rubbing out a whole word, which a Mac types as Alt and everything else as Ctrl.</summary>
    [Fact]
    public void RubbingOutAWordTakesTheWholeWordOff()
    {
        _app.Frame();
        _app.Type(":echo one carrots");
        _app.Press(ConsoleKey.Backspace, alt: true);

        var screen = _app.Frame();

        Assert.Contains("echo one", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("carrots", screen, StringComparison.Ordinal);
    }

    [Fact]
    public void RubbingOutAWordReachesPastTheSpaceTheCaretStandsAfter()
    {
        _app.Frame();
        _app.Type(":echo one carrots ");
        _app.Press(ConsoleKey.Backspace, alt: true);

        var screen = _app.Frame();

        Assert.Contains("echo one", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("carrots", screen, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Moving by word, which a Mac types as Alt and everything else as Ctrl. The framework walks the
    ///     screens on Alt and the arrows, and stands aside for a line that is being typed on.
    /// </summary>
    [Fact]
    public void TheCaretMovesAWordAtATimeUnderBothHabits()
    {
        _app.Frame();
        _app.Type(":echo one two");
        _app.Press(ConsoleKey.LeftArrow, alt: true);
        _app.Type("X");

        Assert.Contains("echo one Xtwo", _app.Frame(), StringComparison.Ordinal);

        _app.Press(ConsoleKey.LeftArrow, control: true);
        _app.Type("Y");

        Assert.Contains("echo one YXtwo", _app.Frame(), StringComparison.Ordinal);
    }

    /// <summary>The way back, which stops at the end of the word rather than at the start of the next.</summary>
    [Fact]
    public void TheCaretComesBackAWordAtATime()
    {
        _app.Frame();
        _app.Type(":echo one two");
        _app.Press(ConsoleKey.LeftArrow, alt: true);
        _app.Press(ConsoleKey.LeftArrow, alt: true);
        _app.Press(ConsoleKey.RightArrow, alt: true);
        _app.Type("X");

        Assert.Contains("echo oneX two", _app.Frame(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     A command too long for one row is carried onto the next rather than scrolled sideways, and the
    ///     panels give up the rows it took. What was carried over is on the rows under the prompt.
    /// </summary>
    [Fact]
    public void ALongCommandIsCarriedOntoTheRowsUnderThePrompt()
    {
        _app.Frame();

        var was = _app.CommandLineRow();

        _app.ReadFromTerminal($"\e[200~echo {string.Join(' ', Enumerable.Repeat("word", 40))} caboose\e[201~");
        _app.Frame();

        var lines = _app.FrameLines();
        var row = _app.CommandLineRow();

        Assert.True(row < was, $"the prompt was on row {was} and is on row {row}");
        Assert.Contains("caboose", string.Concat(lines[(row + 1)..]), StringComparison.Ordinal);
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
