using System;
using System.IO;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Tests.Support;
using Arlecchino.Commander.Views;
using Xunit;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
///     What a search turned up, as it reaches the screen. The hits are put there by hand, and what is
///     asked is what the list looks like and where Enter takes it.
/// </summary>
public sealed class FindScreenTests : IDisposable
{
    private readonly ScreenApp _app = new(ViewKind.Find);

    public void Dispose()
    {
        _app.Dispose();
    }

    private Hit Found(string name)
    {
        var path = _app.Write(name, "anything");

        return new(_app.Folder, new(name, path, false, false, 8, DateTime.Now, false, false, false));
    }

    [Fact]
    public void WithNothingFoundItSaysSo()
    {
        Assert.Contains("nothing found", _app.Frame(), StringComparison.Ordinal);
    }

    [Fact]
    public void EveryHitIsListed()
    {
        _app.Finder.Hits.Add(Found("alpha.txt"));
        _app.Finder.Hits.Add(Found("beta.txt"));

        var screen = _app.Frame();

        Assert.Contains("alpha.txt", screen, StringComparison.Ordinal);
        Assert.Contains("beta.txt", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("nothing found", screen, StringComparison.Ordinal);
    }

    [Fact]
    public void HowManyWereFoundIsSaid()
    {
        _app.Finder.Hits.Add(Found("alpha.txt"));
        _app.Finder.Hits.Add(Found("beta.txt"));

        Assert.Contains("2 found", _app.Frame(), StringComparison.Ordinal);
    }

    [Fact]
    public void MoreHitsThanRowsScrollRatherThanOverflow()
    {
        for (var index = 0; index < 200; index++)
        {
            _app.Finder.Hits.Add(Found($"file-{index}.txt"));
        }

        var lines = _app.FrameLines();

        Assert.Equal(30, lines.Length);
        Assert.All(lines, row => Assert.True(row.Length <= _app.Width));
    }

    [Fact]
    public void EnterOnAHitTakesThePanelToIt()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_app.Folder, "nested")).FullName;
        var path = Path.Combine(folder, "found.txt");
        File.WriteAllText(path, "anything");

        var hit = new Hit(folder, new("found.txt", path, false, false, 8, DateTime.Now, false, false, false));

        _app.Finder.Hits.Add(hit);
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
