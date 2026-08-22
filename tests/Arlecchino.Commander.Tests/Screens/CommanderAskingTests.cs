using System;
using System.Runtime.InteropServices;
using Arlecchino.Commander.Tests.Support;
using Xunit;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
/// Answering a command from the screen it was typed on. There is one way of doing it, the dialog that
/// opens over the panels; the command line runs commands and never carries an answer.
/// </summary>
public sealed class CommanderAskingTests : IDisposable
{
    /// <summary>A command that asks for something and prints whatever it was told.</summary>
    private const string Asking = "printf 'Password: '; read -r answer; echo \"heard $answer\"";

    private readonly ScreenApp _app = Started.Showing();

    public void Dispose() => _app.Dispose();

    [Fact]
    public void TheQuestionOpensOverThePanels()
    {
        if (Windows())
        {
            return;
        }

        Ask();

        var screen = _app.Frame();

        Assert.Contains("Password:", screen, StringComparison.Ordinal);
        Assert.Contains("printf is asking", screen, StringComparison.Ordinal);

        Answer("hunter2");
    }

    /// <summary>
    /// What is typed in answer is drawn as dots, and what the command was told is not on the screen
    /// afterward either — only that something was sent.
    /// </summary>
    [Fact]
    public void WhatIsTypedInAnswerIsNeverOnTheScreen()
    {
        if (Windows())
        {
            return;
        }

        Ask();

        _app.Type("hunter2");

        var screen = _app.Frame();

        Assert.DoesNotContain("hunter2", screen, StringComparison.Ordinal);
        Assert.Contains("•••••••", screen, StringComparison.Ordinal);

        _app.Press(ConsoleKey.Enter);

        Assert.True(_app.Until(() => !_app.Runner.IsRunning));
        Assert.Contains(
            _app.Runner.Lines.Value,
            static line => line.Contains("heard hunter2", StringComparison.Ordinal));
        Assert.DoesNotContain(_app.Runner.Lines.Value, static line => line == "> hunter2");
    }

    /// <summary>
    /// The line goes on being the line. A command that is still running is not typed at from there — what
    /// is typed on it is a command, and Enter says so rather than sending it to whatever is waiting.
    /// </summary>
    [Fact]
    public void TheCommandLineAnswersNothing()
    {
        if (Windows())
        {
            return;
        }

        Ask();
        Escape();

        _app.Type(":hunter2");

        Assert.Contains("hunter2", _app.FrameLines()[_app.CommandLineRow()], StringComparison.Ordinal);

        _app.Press(ConsoleKey.Enter);
        _app.Frame();

        Assert.True(_app.Runner.IsAsking);
        Assert.Contains("still running", _app.State.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(_app.Runner.Lines.Value, static line => line.Contains("hunter2", StringComparison.Ordinal));

        _app.Runner.AskAgain();
        Answer("hunter2");
    }

    private static bool Windows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>Runs the command that asks, from the command line, and waits for the dialog.</summary>
    private void Ask()
    {
        _app.Type(":" + Asking);
        _app.Press(ConsoleKey.Enter);

        Assert.True(_app.Until(() => _app.Runner.IsAsking && _app.State.Modal is not null));
    }

    /// <summary>Types the answer into the dialog and waits for the command to finish on the back of it.</summary>
    /// <param name="text">What to answer.</param>
    private void Answer(string text)
    {
        Assert.NotNull(_app.State.Modal);

        _app.Type(text);
        _app.Press(ConsoleKey.Enter);

        Assert.True(_app.Until(() => !_app.Runner.IsRunning));
    }

    /// <summary>Closes the dialog, which leaves the command waiting rather than answering it.</summary>
    private void Escape()
    {
        _app.Press(ConsoleKey.Escape);
        _app.Frame();

        Assert.Null(_app.State.Modal);
    }
}
