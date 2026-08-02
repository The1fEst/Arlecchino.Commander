using System;
using Arlecchino.Commander.Views;
using Xunit;

namespace Arlecchino.Commander.Tests;

/// <summary>
/// What a command left behind, as the person who ran it sees it. The runner is filled by hand here:
/// what is being asked is how the lines reach the screen, not whether a shell can be started.
/// </summary>
public sealed class OutputScreenTests : IDisposable
{
    private readonly ScreenApp _app = new(ViewKind.Output);

    public void Dispose() => _app.Dispose();

    [Fact]
    public void WithNothingRunItSaysSo()
    {
        Assert.Contains("nothing run yet", _app.Frame(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheLinesACommandLeftAreOnScreen()
    {
        _app.Runner.Lines.Add("first line");
        _app.Runner.Lines.Add("second line");

        var screen = _app.Frame();

        Assert.Contains("first line", screen, StringComparison.Ordinal);
        Assert.Contains("second line", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("nothing run yet", screen, StringComparison.Ordinal);
    }

    [Fact]
    public void MoreLinesThanRowsScrollRatherThanOverflow()
    {
        for (var line = 0; line < 200; line++)
        {
            _app.Runner.Lines.Add($"line {line}");
        }

        var lines = _app.FrameLines();

        Assert.Equal(30, lines.Length);
        Assert.All(lines, static row => Assert.True(row.Length <= 100));
    }

    [Fact]
    public void ClearingTakesTheOutputAway()
    {
        _app.Runner.Lines.Add("something that ran");
        _app.Frame();

        _app.Press(ConsoleKey.K, control: true);

        Assert.DoesNotContain("something that ran", _app.Frame(), StringComparison.Ordinal);
    }

    [Fact]
    public void EscapeGoesBackToThePanels()
    {
        _app.Frame();
        _app.Press(ConsoleKey.Escape);

        Assert.Equal(ViewKind.Commander, _app.Navigator.CurrentRoute);
    }
}
