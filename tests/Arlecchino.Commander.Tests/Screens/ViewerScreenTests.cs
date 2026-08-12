using System;
using System.IO;
using Arlecchino.Commander.Tests.Support;
using Arlecchino.Commander.Views;
using Xunit;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
///     A file as the viewer shows it. The view is built with an empty body and fills in when the read is
///     answered, so every frame here is waited for rather than assumed.
/// </summary>
public sealed class ViewerScreenTests : IDisposable
{
    private readonly ScreenApp _app = new(ViewKind.Viewer);

    public void Dispose()
    {
        _app.Dispose();
    }

    private void Viewing(string name, string text)
    {
        var path = _app.Write(name, text);

        _app.Sessions.Viewing.Value = path;
        _app.Sessions.ViewingSize = new FileInfo(path).Length;
    }

    [Fact]
    public void TheTextOfTheFileIsOnScreen()
    {
        Viewing("notes.txt", "first line\nsecond line\nthird line");

        Assert.True(_app.Shows("first line"));
        Assert.Contains("third line", _app.Frame(), StringComparison.Ordinal);
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

        Assert.True(_app.Shows("xxx"));

        var lines = _app.FrameLines();

        Assert.All(lines, row => Assert.True(row.Length <= _app.Width));
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
