using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Editing;
using Xunit;

namespace Arlecchino.Commander.Tests.Widgets;

/// <summary>
///     What is typed on the command line. The editing itself is the framework's, so what is asserted here is
///     that the line answers it properly: by symbols, by words, and never pointing past its own end.
/// </summary>
public sealed class CommandLineTextTests
{
    [Fact]
    public void AWholeLinePutThereLeavesTheCaretAtTheEnd()
    {
        var text = new CommandLineText { Text = "git status" };

        Assert.Equal(10, text.Caret);
        Assert.Equal(10, text.Anchor);
    }

    [Fact]
    public void ACaretPutPastTheEndIsPulledBackIn()
    {
        var text = new CommandLineText { Text = "git", Caret = 99, Anchor = -4 };

        Assert.Equal(3, text.Caret);
        Assert.Equal(0, text.Anchor);
    }

    [Fact]
    public void AControlCharacterIsRefused()
    {
        var text = new CommandLineText();

        Assert.False(text.Put('\t'));
        Assert.Equal("", text.Text);
    }

    [Fact]
    public void OnlyTheFirstLineOfAPasteLands()
    {
        var text = new CommandLineText();

        text.Paste("echo one\nrm -rf two");

        Assert.Equal("echo one", text.Text);
    }

    [Fact]
    public void RubbingOutTakesAWholeSymbol()
    {
        var text = new CommandLineText { Text = "echo 😀" };

        TextEditing.Backspace(text);

        Assert.Equal("echo ", text.Text);
    }

    [Fact]
    public void RubbingOutAWordReachesPastTheSpacesBeforeIt()
    {
        var text = new CommandLineText { Text = "echo one two   " };

        TextEditing.EraseWord(text);

        Assert.Equal("echo one ", text.Text);
        Assert.Equal(9, text.Caret);
    }

    [Fact]
    public void TheCaretMovesAWordAtATime()
    {
        var text = new CommandLineText { Text = "git commit -m" };

        TextEditing.MoveWord(text, -1);
        Assert.Equal(11, text.Caret);

        TextEditing.MoveWord(text, -1);
        Assert.Equal(4, text.Caret);

        TextEditing.MoveWord(text, 1);
        Assert.Equal(10, text.Caret);
    }

    [Fact]
    public void WhatIsSelectedIsWhatTheCaretWasTakenOver()
    {
        var text = new CommandLineText { Text = "git status" };

        TextEditing.SelectWord(text, -1);

        Assert.Equal("status", TextEditing.Selected(text));
    }
}
