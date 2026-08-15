using System;
using Arlecchino.Commander.Tests.Support;
using Xunit;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
///     Finishing a half-typed word on the command line: what one press of Tab fills in, what the presses
///     after it step through, and what a <c>cd</c> is allowed to be finished with.
/// </summary>
public sealed class CommanderCompletionTests : IDisposable
{
    private readonly ScreenApp _app = Started.Showing();

    public void Dispose()
    {
        _app.Dispose();
    }

    [Fact]
    public void TabFinishesANameFromTheFolderThePanelIsLookingAt()
    {
        _app.Type(":cat al");
        _app.Press(ConsoleKey.Tab);

        Assert.True(Waits("cat alpha.txt"), Line());
    }

    /// <summary>
    ///     Two names that begin alike fill in as far as they agree, which is what a shell does. What each of
    ///     them is in full takes another press.
    /// </summary>
    [Fact]
    public void NamesThatBeginAlikeFillInAsFarAsTheyAgree()
    {
        _app.Write("betamax.txt", "three");
        _app.Type(":cat be");
        _app.Press(ConsoleKey.Tab);

        Assert.True(Waits("cat beta"), Line());

        _app.Press(ConsoleKey.Tab);
        _app.Frame();

        Assert.Contains("cat beta.txt", Line(), StringComparison.Ordinal);

        _app.Press(ConsoleKey.Tab);
        _app.Frame();

        Assert.Contains("cat betamax.txt", Line(), StringComparison.Ordinal);
    }

    [Fact]
    public void WhatWasFoundIsListedOverTheLine()
    {
        _app.Write("betamax.txt", "three");
        _app.Type(":cat be");
        _app.Frame();

        Assert.False(Boxed(), _app.Frame());

        _app.Press(ConsoleKey.Tab);

        Assert.True(_app.Until(Boxed), _app.Frame());
    }

    /// <summary>A folder is offered with the separator after it, so the next press carries on inside it.</summary>
    [Fact]
    public void AFolderIsFinishedWithTheSeparatorAfterIt()
    {
        _app.Type(":cd ne");
        _app.Press(ConsoleKey.Tab);

        Assert.True(Waits("cd nested/"), Line());
    }

    /// <summary>A <c>cd</c> can only go to a folder, so the files in the way are no answer to it.</summary>
    [Fact]
    public void AChangeOfFolderIsNotFinishedWithAFile()
    {
        _app.Type(":cd al");
        _app.Press(ConsoleKey.Tab);
        _app.Frame();

        Assert.DoesNotContain("alpha.txt", Line(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Whether the box of what was found is standing over the panels. It is known by its heading, which
    ///     the band along the top says too and is told from it by the name of the application beside it.
    /// </summary>
    /// <returns><c>true</c> when the box is drawn.</returns>
    private bool Boxed()
    {
        foreach (var line in _app.FrameLines())
        {
            if (line.Contains("COMMAND", StringComparison.Ordinal) &&
                !line.Contains("ARLECCHINO", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Draws frames until the line reads a certain way, since a folder is read off the drawing thread.</summary>
    /// <param name="text">What the line should say.</param>
    /// <returns><c>true</c> when it came to say it.</returns>
    private bool Waits(string text) =>
        _app.Until(() => Line().Contains(text, StringComparison.Ordinal));

    /// <summary>The row the command line is drawn on.</summary>
    /// <returns>What it says.</returns>
    private string Line() => _app.FrameLines()[_app.CommandLineRow()];
}
