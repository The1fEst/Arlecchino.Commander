using System;
using System.IO;
using System.Linq;
using Arlecchino.Commander.Views;
using Xunit;

using Arlecchino.Commander.Tests.Support;

namespace Arlecchino.Commander.Tests.Screens;

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

        _app.Sessions.Start(_app.Folder, _app.Folder);
        _app.Settled();
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

    /// <summary>
    /// Every operation is asked through the same dialog, so every one of them names itself, says what
    /// it will act on, and offers the same two keys to answer with.
    /// </summary>
    [Fact]
    public void CopyingAsksBeforeItCopies()
    {
        OnAlpha();
        _app.Press(ConsoleKey.F5);

        var screen = _app.Frame();

        Assert.Contains("Copy", screen, StringComparison.Ordinal);
        Assert.Contains("WHERE", screen, StringComparison.Ordinal);
        Assert.Contains("Enter Copy", screen, StringComparison.Ordinal);
        Assert.Contains("Esc Cancel", screen, StringComparison.Ordinal);
    }

    [Fact]
    public void MakingAFolderAsksWhatToCallIt()
    {
        _app.Press(ConsoleKey.F7);

        var screen = _app.Frame();

        Assert.Contains("New folder", screen, StringComparison.Ordinal);
        Assert.Contains("NAME", screen, StringComparison.Ordinal);
        Assert.Contains("Enter Create", screen, StringComparison.Ordinal);
    }

    /// <summary>Nothing is deleted by the asking, and the dialog says there is no undoing it.</summary>
    [Fact]
    public void DeletingAsksBeforeItDeletes()
    {
        OnAlpha();
        _app.Press(ConsoleKey.F8);

        var screen = _app.Frame();

        Assert.Contains("Delete", screen, StringComparison.Ordinal);
        Assert.Contains("GOING AWAY", screen, StringComparison.Ordinal);
        Assert.Contains("no undoing it", screen, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_app.Folder, "alpha.txt")));
    }

    /// <summary>Escape leaves everything as it was, which is what makes the dialog safe to open.</summary>
    [Fact]
    public void CallingTheDialogOffChangesNothing()
    {
        OnAlpha();
        _app.Press(ConsoleKey.F8);
        _app.Frame();
        _app.Press(ConsoleKey.Escape);

        var screen = _app.Frame();

        Assert.DoesNotContain("GOING AWAY", screen, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_app.Folder, "alpha.txt")));
    }

    /// <summary>What is typed into the one field is what the operation is given.</summary>
    [Fact]
    public void TheFolderIsMadeUnderTheNameThatWasTyped()
    {
        _app.Press(ConsoleKey.F7);
        _app.Frame();
        _app.Type("benchmarks");
        _app.Press(ConsoleKey.Enter);

        Assert.True(_app.Until(() => Directory.Exists(Path.Combine(_app.Folder, "benchmarks"))));
    }

    /// <summary>
    /// Tab finishes the path in the field, to as much as every candidate agrees on. Typing a
    /// destination out in full is the slowest thing the dialog ever asks for.
    /// </summary>
    [Fact]
    public void TabFinishesThePathInTheField()
    {
        Directory.CreateDirectory(Path.Combine(_app.Folder, "nestling"));

        _app.Sessions.Left.Marks.Clear();
        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.F5);
        _app.Frame();
        _app.Type("/nestl");
        _app.Press(ConsoleKey.Tab);

        Assert.True(_app.Until(() => _app.Frame().Contains("nestling", StringComparison.Ordinal)));
    }

    /// <summary>
    /// It completes to as much as the candidates agree on and no further. Two folders that start alike
    /// are a question the field cannot answer, so it answers the part it can.
    /// </summary>
    [Fact]
    public void TabStopsWhereTheNamesStopAgreeing()
    {
        Directory.CreateDirectory(Path.Combine(_app.Folder, "nestling"));

        _app.Sessions.Left.Marks.Clear();
        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.F5);
        _app.Frame();
        _app.Type("/nes");
        _app.Press(ConsoleKey.Tab);
        _app.Until(() => false);

        var field = Array.Find(_app.FrameLines(), line => line.Contains("/nes", StringComparison.Ordinal));

        Assert.NotNull(field);
        Assert.Contains("/nest", field, StringComparison.Ordinal);
        Assert.DoesNotContain("nestling", field, StringComparison.Ordinal);
        Assert.DoesNotContain("nested", field, StringComparison.Ordinal);
    }

    /// <summary>Tab reaches the switches and Space turns them, which is the whole of the dialog's input.</summary>
    [Fact]
    public void TabReachesTheSwitchesAndSpaceTurnsThem()
    {
        _app.Press(ConsoleKey.F7);
        _app.Frame();

        Assert.Contains("[×] jump the cursor onto it", _app.Frame(), StringComparison.Ordinal);

        _app.Press(ConsoleKey.Tab);
        _app.Press(ConsoleKey.Spacebar);

        Assert.Contains("[ ] jump the cursor onto it", _app.Frame(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The palette is the way to everything the bar along the bottom has no room for. It holds the
    /// menu entry by entry as well as the keys, so nothing has to be found by remembering where it was
    /// filed.
    /// </summary>
    [Fact]
    public void ThePaletteHoldsEverythingTheBarDoesNot()
    {
        _app.Press(ConsoleKey.K, control: true);

        var screen = _app.Frame();

        Assert.Contains("Do anything", screen, StringComparison.Ordinal);
        Assert.Contains("Find file", screen, StringComparison.Ordinal);
        Assert.Contains("Enter run", screen, StringComparison.Ordinal);
        Assert.Contains("Tab complete", screen, StringComparison.Ordinal);
    }

    /// <summary>Typing narrows it, and the count says by how much.</summary>
    [Fact]
    public void TypingNarrowsThePalette()
    {
        _app.Press(ConsoleKey.K, control: true);
        _app.Frame();
        _app.Type("hotlist");

        var screen = _app.Frame();

        Assert.Contains("Hotlist", screen, StringComparison.Ordinal);
        Assert.Contains(" of ", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("Find file", screen, StringComparison.Ordinal);
    }

    /// <summary>Picking a row runs it, which is the whole point of a list of actions.</summary>
    [Fact]
    public void PickingFromThePaletteRunsIt()
    {
        _app.Press(ConsoleKey.K, control: true);
        _app.Frame();
        _app.Type("hidden files here");
        _app.Press(ConsoleKey.Enter);

        Assert.True(_app.Until(() => _app.Frame().Contains(".hidden", StringComparison.Ordinal)));
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

        _app.Sessions.Right.GoTo(other);
        _app.Frame();

        var left = _app.Sessions.Left.Folder;
        var right = _app.Sessions.Right.Folder;

        _app.Press(ConsoleKey.U, control: true);
        _app.Frame();

        Assert.Equal(right, _app.Sessions.Left.Folder);
        Assert.Equal(left, _app.Sessions.Right.Folder);
    }

    [Fact]
    public void ReloadingKeepsThePanelWhereItWas()
    {
        var where = _app.Sessions.Left.Folder;

        _app.Write("appeared.txt", "three");
        _app.Press(ConsoleKey.R, control: true);

        var screen = _app.Frame();

        Assert.Equal(where, _app.Sessions.Left.Folder);
        Assert.Contains("appeared.txt", screen, StringComparison.Ordinal);
    }

    /// <summary>
    /// The way out of a folder is the <c>..</c> row, and only that. Backspace used to do it as well and
    /// no longer does: the command line is typed on without ever taking the focus, so a Backspace meant
    /// for a typo would leave the folder instead.
    /// </summary>
    [Fact]
    public void GoingUpLeavesTheFolder()
    {
        var nested = Path.Combine(_app.Folder, "nested");

        _app.Sessions.Left.GoTo(nested);
        _app.Sessions.Moved();
        _app.Settled();

        _app.Settled();
        _app.Press(ConsoleKey.Home);
        _app.Press(ConsoleKey.Enter);

        Assert.True(_app.Until(() => _app.Sessions.Left.Folder == _app.Folder));
    }

    [Fact]
    public void BackspaceIsLeftToTheCommandLine()
    {
        var nested = Path.Combine(_app.Folder, "nested");

        _app.Sessions.Left.GoTo(nested);
        _app.Sessions.Moved();
        _app.Settled();

        _app.Type("ls x");
        _app.Press(ConsoleKey.Backspace);

        Assert.Equal(nested, _app.Sessions.Left.Folder);
        Assert.Contains("ls ", _app.Frame(), StringComparison.Ordinal);
        Assert.DoesNotContain("ls x", _app.Frame(), StringComparison.Ordinal);
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

        foreach (var key in new[] { "F3", "F5", "F8" })
        {
            Assert.Contains(key, bar, StringComparison.Ordinal);
        }

        Assert.Contains("view", known);
        Assert.Contains("copy", known);
        Assert.Contains("delete", known);
    }

    /// <summary>
    /// Escape is bound to stopping the work, and most of the time there is no work. The command says
    /// so and stands aside, which is what lets the same key end the search that runs while you type —
    /// one key for "get me out of this", whichever thing there is to get out of.
    /// </summary>
    [Fact]
    public void EscapeEndsTheSearchWhenThereIsNothingToStop()
    {
        _app.Press(ConsoleKey.S, control: true);
        _app.Type("al");

        Assert.Contains("jump to", _app.Frame(), StringComparison.Ordinal);

        _app.Press(ConsoleKey.Escape);

        Assert.DoesNotContain("jump to", _app.Frame(), StringComparison.Ordinal);
    }
}
