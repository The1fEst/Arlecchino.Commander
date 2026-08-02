using System;
using System.IO;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Views;
using Xunit;

using Arlecchino.Commander.Tests.Support;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
/// What a search turned up, as it reaches the screen. The hits are put there by hand: whether a disk
/// can be walked is the finder's business and is tested elsewhere, and what is asked here is what the
/// list looks like and where Enter takes it.
/// </summary>
public sealed class FindScreenTests : IDisposable
{
    private readonly ScreenApp _app = new(ViewKind.Find);

    public void Dispose() => _app.Dispose();

    private Hit Found(string name)
    {
        var path = _app.Write(name, "anything");

        return new(_app.Folder, new(name, path, false, false, 8, DateTime.Now, false, false));
    }

    [Fact]
    public void WithNothingFoundItSaysSo()
    {
        Assert.Contains("nothing found", _app.Frame(), StringComparison.Ordinal);
    }

    [Fact]
    public void EveryHitIsListed()
    {
        _app.Finder.Found.Add(Found("alpha.txt"));
        _app.Finder.Found.Add(Found("beta.txt"));

        var screen = _app.Frame();

        Assert.Contains("alpha.txt", screen, StringComparison.Ordinal);
        Assert.Contains("beta.txt", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("nothing found", screen, StringComparison.Ordinal);
    }

    [Fact]
    public void HowManyWereFoundIsSaid()
    {
        _app.Finder.Found.Add(Found("alpha.txt"));
        _app.Finder.Found.Add(Found("beta.txt"));

        Assert.Contains("2 found", _app.Frame(), StringComparison.Ordinal);
    }

    [Fact]
    public void MoreHitsThanRowsScrollRatherThanOverflow()
    {
        for (var hit = 0; hit < 200; hit++)
        {
            _app.Finder.Found.Add(Found($"file-{hit}.txt"));
        }

        var lines = _app.FrameLines();

        Assert.Equal(30, lines.Length);
        Assert.All(lines, static row => Assert.True(row.Length <= 100));
    }

    [Fact]
    public void EnterOnAHitTakesThePanelToIt()
    {
        var nested = Directory.CreateDirectory(Path.Combine(_app.Folder, "nested")).FullName;
        var path = Path.Combine(nested, "found.txt");
        File.WriteAllText(path, "anything");

        var hit = new Hit(nested, new("found.txt", path, false, false, 8, DateTime.Now, false, false));

        _app.Finder.Found.Add(hit);
        _app.Frame();

        _app.Press(ConsoleKey.Enter);

        Assert.Equal(ViewKind.Commander, _app.Navigator.CurrentRoute);
        Assert.EndsWith("nested", _app.Sessions.Left.Folder, StringComparison.Ordinal);
    }

    [Fact]
    public void EscapeGoesBackToThePanels()
    {
        _app.Frame();
        _app.Press(ConsoleKey.Escape);

        Assert.Equal(ViewKind.Commander, _app.Navigator.CurrentRoute);
    }
}
