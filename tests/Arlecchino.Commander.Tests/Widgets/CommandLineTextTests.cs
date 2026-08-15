using Arlecchino.Commander.Widgets.Chrome;
using Xunit;

namespace Arlecchino.Commander.Tests.Widgets;

/// <summary>
///     What is typed on the command line. The editing itself is the framework's, so what is asserted here is
///     that the line is edited by symbols and by words rather than by <c>char</c> values.
/// </summary>
public sealed class CommandLineTextTests
{
    [Fact]
    public void AWholeLinePutThereLeavesTheCaretAtTheEnd()
    {
        var text = new CommandLineText { Text = "git status" };

        Assert.Equal(10, text.Caret);
    }

    [Fact]
    public void RubbingOutTakesAWholeSymbol()
    {
        var text = new CommandLineText { Text = "echo 😀" };

        text.Back();

        Assert.Equal("echo ", text.Text);
    }

    [Fact]
    public void TheCaretSteppedLeftLandsBeforeAWholeSymbol()
    {
        var text = new CommandLineText { Text = "😀x" };

        text.Left();
        text.Left();

        Assert.Equal(0, text.Caret);
    }

    [Fact]
    public void RubbingOutAWordReachesPastTheSpacesBeforeIt()
    {
        var text = new CommandLineText { Text = "echo one two   " };

        text.Word();

        Assert.Equal("echo one ", text.Text);
        Assert.Equal(9, text.Caret);
    }

    [Fact]
    public void TheCaretMovesAWordAtATime()
    {
        var text = new CommandLineText { Text = "git commit -m" };

        text.WordLeft();
        Assert.Equal(11, text.Caret);

        text.WordLeft();
        Assert.Equal(4, text.Caret);

        text.WordRight();
        Assert.Equal(10, text.Caret);
    }
}
