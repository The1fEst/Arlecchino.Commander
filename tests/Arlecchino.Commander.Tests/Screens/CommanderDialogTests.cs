using System;
using System.IO;
using Arlecchino.Commander.Tests.Support;
using Xunit;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
///     The dialogs this screen opens, worked with the mouse. A button that can only be pressed by the key
///     printed on it is a picture of a button, so what is asserted here is the clicking.
/// </summary>
public sealed class CommanderDialogTests : IDisposable
{
    private readonly ScreenApp _app = Started.Showing();

    public void Dispose()
    {
        _app.Dispose();
    }

    /// <summary>The button that says <c>Enter Make</c> makes the folder when it is clicked.</summary>
    [Fact]
    public void ClickingTheConfirmButtonAnswersTheQuestion()
    {
        _app.Press(ConsoleKey.F7);
        _app.Frame();
        _app.Type("clicked");

        Click(_app.FrameLines(), "Enter ");
        _app.Settled();

        Assert.Null(_app.State.Modal);
        Assert.True(Directory.Exists(Path.Combine(_app.Folder, "clicked")));
    }

    /// <summary>And the one beside it closes the question without answering it.</summary>
    [Fact]
    public void ClickingTheCancelButtonCallsItOff()
    {
        _app.Press(ConsoleKey.F7);
        _app.Frame();
        _app.Type("never");

        Click(_app.FrameLines(), "Esc Cancel");
        _app.Settled();

        Assert.Null(_app.State.Modal);
        Assert.False(Directory.Exists(Path.Combine(_app.Folder, "never")));
    }

    /// <summary>
    ///     A row of a list is selected by a click and run by a second one on the row already selected,
    ///     which is the rule everywhere else a list is clicked.
    /// </summary>
    [Fact]
    public void ClickingARowTwiceRunsIt()
    {
        _app.Sessions.Add();
        _app.Press(ConsoleKey.F2);
        _app.Frame();

        Assert.Equal(1, _app.Sessions.Open.Value);

        Click(_app.FrameLines(), "1 · local");
        _app.Frame();
        Click(_app.FrameLines(), "1 · local");
        _app.Settled();

        Assert.Null(_app.State.Modal);
        Assert.Equal(0, _app.Sessions.Open.Value);
    }

    /// <summary>Clicking away changes nothing: a stray click must not discard what was typed.</summary>
    [Fact]
    public void ClickingOutsideADialogLeavesItOpen()
    {
        _app.Press(ConsoleKey.F7);
        _app.Frame();
        _app.Type("kept");

        _app.Click(0, 0);

        var screen = _app.Frame();

        Assert.NotNull(_app.State.Modal);
        Assert.Contains("kept", screen, StringComparison.Ordinal);
    }

    /// <summary>Clicks where some text is drawn, wherever the dialog happened to place it.</summary>
    /// <param name="lines">The frame as it was drawn.</param>
    /// <param name="text">What to click on.</param>
    private void Click(string[] lines, string text)
    {
        for (var row = 0; row < lines.Length; row++)
        {
            var at = lines[row].IndexOf(text, StringComparison.Ordinal);

            if (at < 0)
            {
                continue;
            }

            _app.Click(row, at);

            return;
        }

        Assert.Fail($"'{text}' is not on the screen.");
    }
}
