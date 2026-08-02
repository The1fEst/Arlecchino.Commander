using System;
using System.IO;
using Arlecchino.Commander.Views;
using Xunit;

namespace Arlecchino.Commander.Tests;

/// <summary>
/// A file as the viewer shows it. What it is told to show is read as the view is built, so the state
/// goes in before the first frame is asked for.
/// </summary>
public sealed class ViewerScreenTests : IDisposable
{
    private readonly ScreenApp _app = new(ViewKind.Viewer);

    public void Dispose() => _app.Dispose();

    private void Viewing(string name, string text)
    {
        var path = _app.Write(name, text);

        _app.Panels.Viewing.Value = path;
        _app.Panels.ViewingSize = new FileInfo(path).Length;
    }

    [Fact]
    public void TheTextOfTheFileIsOnScreen()
    {
        Viewing("notes.txt", "first line\nsecond line\nthird line");

        var screen = _app.Frame();

        Assert.Contains("first line", screen, StringComparison.Ordinal);
        Assert.Contains("third line", screen, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNameOfTheFileIsInTheChrome()
    {
        Viewing("notes.txt", "anything");

        Assert.Contains("notes.txt", _app.Frame(), StringComparison.Ordinal);
    }

    [Fact]
    public void ALineWiderThanTheScreenDoesNotSpillOverIt()
    {
        Viewing("wide.txt", new('x', 400));

        var lines = _app.FrameLines();

        Assert.All(lines, static row => Assert.True(row.Length <= 100));
    }

    [Fact]
    public void AFileOfNothingStillDraws()
    {
        Viewing("empty.txt", "");

        Assert.Equal(30, _app.FrameLines().Length);
    }

    [Fact]
    public void EscapeGoesBackToThePanels()
    {
        Viewing("notes.txt", "anything");
        _app.Frame();

        _app.Press(ConsoleKey.Escape);

        Assert.Equal(ViewKind.Commander, _app.Navigator.CurrentRoute);
    }
}
