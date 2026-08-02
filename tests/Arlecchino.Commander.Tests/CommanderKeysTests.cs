using System;
using System.IO;
using System.Linq;
using Arlecchino.Commander.Views;
using Xunit;

namespace Arlecchino.Commander.Tests;

/// <summary>
/// The function keys along the bottom, pressed. Each of them either changes the screen, opens something
/// to answer, or refuses — and refusing quietly, with nothing on screen to say why, is the failure worth
/// catching.
/// </summary>
public sealed class CommanderKeysTests : IDisposable
{
    private readonly ScreenApp _app = new(ViewKind.Commander);

    public CommanderKeysTests()
    {
        _app.Write("alpha.txt", "one");
        _app.Write(".hidden", "two");
        Directory.CreateDirectory(Path.Combine(_app.Folder, "nested"));

        _app.Panels.Start(_app.Folder, _app.Folder);
        _app.Frame();
    }

    public void Dispose() => _app.Dispose();

    private void OnAlpha()
    {
        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.DownArrow);
        _app.Frame();
    }

    [Fact]
    public void ViewingAFileGoesToTheViewer()
    {
        OnAlpha();
        _app.Press(ConsoleKey.F3);
        _app.Frame();

        Assert.Equal(ViewKind.Viewer, _app.Navigator.CurrentRoute);
    }

    [Fact]
    public void CopyingAsksBeforeItCopies()
    {
        OnAlpha();
        _app.Press(ConsoleKey.F5);
        _app.Frame();

        Assert.NotNull(_app.State.Modal);
    }

    [Fact]
    public void MakingAFolderAsksWhatToCallIt()
    {
        _app.Press(ConsoleKey.F7);
        _app.Frame();

        Assert.NotNull(_app.State.Modal);
    }

    [Fact]
    public void DeletingAsksBeforeItDeletes()
    {
        OnAlpha();
        _app.Press(ConsoleKey.F8);
        _app.Frame();

        Assert.NotNull(_app.State.Modal);
        Assert.True(File.Exists(Path.Combine(_app.Folder, "alpha.txt")));
    }

    [Fact]
    public void TheMenuOpensOnTheKeyItIsLabelledWith()
    {
        _app.Press(ConsoleKey.F9);
        _app.Frame();

        Assert.NotNull(_app.State.Modal);
    }

    [Fact]
    public void FilteringAsksForThePattern()
    {
        _app.Press(ConsoleKey.F4);
        _app.Frame();

        Assert.NotNull(_app.State.Modal);
    }

    [Fact]
    public void HiddenFilesAreShownAndHiddenAgain()
    {
        Assert.DoesNotContain(".hidden", _app.Frame(), StringComparison.Ordinal);

        _app.Press(ConsoleKey.H, control: true);
        Assert.Contains(".hidden", _app.Frame(), StringComparison.Ordinal);

        _app.Press(ConsoleKey.H, control: true);
        Assert.DoesNotContain(".hidden", _app.Frame(), StringComparison.Ordinal);
    }

    [Fact]
    public void SwappingPutsEachPanelWhereTheOtherWas()
    {
        var other = Directory.CreateDirectory(Path.Combine(_app.Folder, "other")).FullName;

        _app.Panels.Right.GoTo(other);
        _app.Frame();

        var left = _app.Panels.Left.Folder;
        var right = _app.Panels.Right.Folder;

        _app.Press(ConsoleKey.U, control: true);
        _app.Frame();

        Assert.Equal(right, _app.Panels.Left.Folder);
        Assert.Equal(left, _app.Panels.Right.Folder);
    }

    [Fact]
    public void ReloadingKeepsThePanelWhereItWas()
    {
        var where = _app.Panels.Left.Folder;

        _app.Write("appeared.txt", "three");
        _app.Press(ConsoleKey.R, control: true);

        var screen = _app.Frame();

        Assert.Equal(where, _app.Panels.Left.Folder);
        Assert.Contains("appeared.txt", screen, StringComparison.Ordinal);
    }

    [Fact]
    public void GoingUpLeavesTheFolder()
    {
        var nested = Path.Combine(_app.Folder, "nested");

        _app.Panels.Left.GoTo(nested);
        _app.Frame();

        _app.Press(ConsoleKey.Backspace);
        _app.Frame();

        Assert.Equal(_app.Folder, _app.Panels.Left.Folder);
    }

    /// <summary>
    /// Every key the bar advertises has something behind it. A label with nothing behind it is a key
    /// that does nothing when it is pressed, and the bar is the only instruction most people read.
    /// </summary>
    [Fact]
    public void EveryKeyTheBarAdvertisesIsOneTheViewKnows()
    {
        var bar = _app.FrameLines()[^1];
        var known = _app.Navigator.CurrentCommands.Select(static command => command.Label()).ToList();

        foreach (var label in new[] { "Help", "Menu", "View", "Filter", "Copy", "Move", "Mkdir", "Delete", "Quit" })
        {
            Assert.Contains(label, bar, StringComparison.Ordinal);
        }

        Assert.Contains("view", known);
        Assert.Contains("copy", known);
        Assert.Contains("delete", known);
    }
}
