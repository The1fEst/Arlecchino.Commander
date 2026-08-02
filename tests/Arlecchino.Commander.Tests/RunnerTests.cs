using System;
using Arlecchino.Commander.Files;
using Arlecchino.Commander.Views;
using Xunit;

namespace Arlecchino.Commander.Tests;

/// <summary>
/// Running a command and keeping what it said. A real shell is started here: what a shell prints is
/// the whole point, and a stub of one would only report what the stub was told to say.
/// </summary>
public sealed class RunnerTests : IDisposable
{
    private readonly ScreenApp _app = new(ViewKind.Output);
    private readonly LocalSource _source = new();

    public void Dispose() => _app.Dispose();

    private bool Run(string command)
    {
        var done = false;

        _app.Runner.Run(command, _app.Folder, _source, () => done = true);

        return _app.Until(() => done);
    }

    [Fact]
    public void WhatTheCommandPrintedIsKept()
    {
        Assert.True(Run("echo hello"));

        Assert.Contains(_app.Runner.Lines.Value, static line => line.Contains("hello", StringComparison.Ordinal));
    }

    [Fact]
    public void TheCommandItselfIsWrittenAboveWhatItSaid()
    {
        Assert.True(Run("echo hello"));

        Assert.Equal("$ echo hello", _app.Runner.Lines.Value[0]);
        Assert.Equal("echo hello", _app.Runner.Last);
    }

    [Fact]
    public void WhatWasRunIsRemembered()
    {
        Assert.True(Run("echo one"));
        Assert.True(Run("echo two"));

        Assert.Contains("echo one", _app.Runner.History);
        Assert.Contains("echo two", _app.Runner.History);
    }

    [Fact]
    public void ItIsNotRunningOnceItHasFinished()
    {
        Assert.True(Run("echo hello"));

        Assert.False(_app.Runner.IsRunning);
    }

    [Fact]
    public void AFailureIsKeptRatherThanThrown()
    {
        Assert.True(Run("this-command-does-not-exist"));

        Assert.False(_app.Runner.IsRunning);
        Assert.True(_app.Runner.Lines.Count > 1, "the shell said nothing at all about a command it could not run");
    }

    [Fact]
    public void ClearingLeavesNothingBehind()
    {
        Assert.True(Run("echo hello"));

        _app.Runner.Clear();

        Assert.Empty(_app.Runner.Lines.Value);
    }

    [Fact]
    public void WhatItPrintedReachesTheScreen()
    {
        Assert.True(Run("echo hello"));

        Assert.Contains("hello", _app.Frame(), StringComparison.Ordinal);
    }
}
