using System;
using System.Runtime.InteropServices;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Views;
using Xunit;
using Arlecchino.Commander.Tests.Support;

namespace Arlecchino.Commander.Tests.Stores;

/// <summary>
/// A command that stops to ask something. A real shell is started here as it is for the rest of the
/// runner, since what is being asserted is that a question reaches the user and an answer reaches back.
/// </summary>
public sealed class RunnerAskingTests : IDisposable
{
    /// <summary>A command that asks for something and prints whatever it was told.</summary>
    private const string Asking = "printf 'Password: '; read -r answer; echo \"heard $answer\"";

    private readonly ScreenApp _app = new(ViewKind.Output);
    private readonly LocalSource _source = new();

    private bool _done;

    public void Dispose() => _app.Dispose();

    [Fact]
    public void AQuestionTheCommandStoppedOnIsWaitedOn()
    {
        if (Windows())
        {
            return;
        }

        Run(Asking);

        Assert.True(_app.Until(() => _app.Runner.IsAsking));
        Assert.Contains("Password:", _app.Runner.Asking, StringComparison.Ordinal);

        Answer("hunter2");
    }

    [Fact]
    public void ItIsPutInADialogWhereverTheUserIs()
    {
        if (Windows())
        {
            return;
        }

        Run(Asking);

        Assert.True(_app.Until(() => _app.State.Modal is not null));
        Assert.Contains("Password:", _app.Frame(), StringComparison.Ordinal);

        Answer("hunter2");
    }

    [Fact]
    public void WhatIsAnsweredReachesTheCommand()
    {
        if (Windows())
        {
            return;
        }

        Run(Asking);

        Assert.True(_app.Until(() => _app.Runner.IsAsking));

        Answer("hunter2");

        Assert.Contains(_app.Runner.Lines.Value, static line => line.Contains("heard hunter2", StringComparison.Ordinal));
    }

    [Fact]
    public void WhatWasAnsweredIsNotWrittenIntoTheRoll()
    {
        if (Windows())
        {
            return;
        }

        Run(Asking);

        Assert.True(_app.Until(() => _app.Runner.IsAsking));

        _app.Runner.Answer("hunter2");

        Assert.False(_app.Runner.IsAsking);
        Assert.True(_app.Until(() => _done));

        Assert.Contains(_app.Runner.Lines.Value, static line => line.StartsWith("> •", StringComparison.Ordinal));
        Assert.DoesNotContain(_app.Runner.Lines.Value, static line => line == "> hunter2");
    }

    /// <summary>
    /// What a command printed arrives while it is still going, rather than in one piece at the end. The
    /// question is only ever seen because of it: the command that asked has not finished.
    /// </summary>
    [Fact]
    public void WhatItPrintedArrivesWhileItIsStillRunning()
    {
        if (Windows())
        {
            return;
        }

        Run("echo first; " + Asking);

        Assert.True(_app.Until(() => _app.Runner.IsAsking));
        Assert.Contains(_app.Runner.Lines.Value, static line => line.Contains("first", StringComparison.Ordinal));
        Assert.True(_app.Runner.IsRunning);

        Answer("hunter2");
    }

    /// <summary>
    /// The input is left open, which is what lets an answer reach a command at all. One reading a file
    /// from it waits until it is told there will be none, as it waits for <c>Ctrl+D</c> at a terminal.
    /// </summary>
    [Fact]
    public void SayingThereIsNoMoreInputEndsACommandReadingIt()
    {
        if (Windows())
        {
            return;
        }

        Run("cat");

        Assert.True(_app.Until(() => _app.Runner.IsRunning));

        _app.Runner.EndInput();

        Assert.True(_app.Until(() => _done));
        Assert.False(_app.Runner.IsRunning);
    }

    /// <summary>
    /// A command asks twice where the first answer was not the one it wanted, which is what a password
    /// typed wrongly comes to. The second question stands on its own rather than running on from the first.
    /// </summary>
    [Fact]
    public void AQuestionAskedAgainIsAskedAgain()
    {
        if (Windows())
        {
            return;
        }

        Run("printf 'Password: '; read -r one; printf 'Password: '; read -r two; echo \"heard $one $two\"");

        Assert.True(_app.Until(() => _app.Runner.IsAsking));
        Assert.Equal("Password:", _app.Runner.Asking);

        _app.Runner.Answer("first");

        Assert.True(_app.Until(() => _app.Runner.IsAsking));
        Assert.Equal("Password:", _app.Runner.Asking);

        Answer("second");

        Assert.Contains(
            _app.Runner.Lines.Value,
            static line => line.Contains("heard first second", StringComparison.Ordinal));
    }

    [Fact]
    public void NothingIsAnsweredWhenNothingIsRunning()
    {
        _app.Runner.Answer("hunter2");

        Assert.Contains("Nothing is running", _app.State.Output, StringComparison.Ordinal);
    }

    private static bool Windows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private void Run(string command) => _app.Runner.Run(command, _app.Folder, _source, () => _done = true);

    /// <summary>Answers the standing question and waits for the command to finish on the back of it.</summary>
    /// <param name="text">What to send.</param>
    private void Answer(string text)
    {
        _app.Runner.Answer(text);

        Assert.True(_app.Until(() => _done));
    }
}
