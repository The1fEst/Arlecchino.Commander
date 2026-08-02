using System;
using Arlecchino.Commander.Views;
using Xunit;

namespace Arlecchino.Commander.Tests;

/// <summary>
/// The shell over a connection, with no connection. That is the state worth pinning: it must say so
/// plainly rather than draw an empty pane or reach for a session that is not there.
/// </summary>
public sealed class SshScreenTests : IDisposable
{
    private readonly ScreenApp _app = new(ViewKind.Ssh);

    public void Dispose() => _app.Dispose();

    [Fact]
    public void WithNothingConnectedItSaysWhatIsMissing()
    {
        Assert.Contains("Connect a panel over sftp first", _app.Frame(), StringComparison.Ordinal);
    }

    [Fact]
    public void WithNothingRunTheOutputSaysSo()
    {
        Assert.Contains("nothing run yet", _app.Frame(), StringComparison.Ordinal);
    }

    /// <summary>
    /// With no session there is nothing to run on, and the view says so instead of asking for a command
    /// it could not send anywhere.
    /// </summary>
    [Fact]
    public void RunningWithNoSessionIsRefusedRatherThanAttempted()
    {
        _app.Frame();

        _app.Press(ConsoleKey.Enter);

        Assert.Equal("Connect a panel over sftp first", _app.State.Output);
        Assert.Null(_app.State.Modal);
    }

    [Fact]
    public void ItDrawsTheWholeScreenWithoutSpillingOverIt()
    {
        var lines = _app.FrameLines();

        Assert.Equal(30, lines.Length);
        Assert.All(lines, static row => Assert.True(row.Length <= 100));
    }

    [Fact]
    public void EscapeGoesBackToThePanels()
    {
        _app.Frame();
        _app.Press(ConsoleKey.Escape);

        Assert.Equal(ViewKind.Commander, _app.Navigator.CurrentRoute);
    }
}
