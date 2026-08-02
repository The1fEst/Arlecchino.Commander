using System;
using System.IO;
using System.Linq;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Views;
using Arlecchino.Commander.Widgets.Chrome;
using Xunit;

using Arlecchino.Commander.Tests.Support;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
/// The panels as they reach the screen. Everything else here is about parsing what a server or a disk
/// said; this is about what the person in front of the terminal ends up looking at, which until now
/// nothing asserted.
/// </summary>
public sealed class CommanderScreenTests : IDisposable
{
    private readonly ScreenApp _app = new(ViewKind.Commander);

    public CommanderScreenTests()
    {
        _app.Write("alpha.txt", "one");
        _app.Write("beta.txt", "two");
        Directory.CreateDirectory(Path.Combine(_app.Folder, "nested"));

        _app.Panels.Start(_app.Folder, _app.Folder);
        _app.Settled();
    }

    public void Dispose() => _app.Dispose();

    [Fact]
    public void BothPanelsShowWhatIsInTheFolder()
    {
        var screen = _app.Frame();

        Assert.Contains("alpha.txt", screen, StringComparison.Ordinal);
        Assert.Contains("beta.txt", screen, StringComparison.Ordinal);
        Assert.Contains("nested", screen, StringComparison.Ordinal);

        Assert.Equal(2, Occurrences(screen, "alpha.txt"));
    }

    /// <summary>
    /// The one thing a frame read as text cannot answer. A folder and a file read the same; what tells
    /// them apart is the colour, and the colour is on the screen rather than in the words.
    /// </summary>
    [Fact]
    public void AFolderIsDrawnInTheColourFoldersGet()
    {
        _app.Frame();

        Assert.Equal(Skin.Lively.Remote.Ansi, _app.StyleOf("dir"));
        Assert.Equal(Skin.Lively.Faded.Ansi, _app.StyleOf("txt"));
        Assert.Equal(Skin.Lively.Text.Ansi, _app.StyleOf("alpha.txt"));
    }

    [Fact]
    public void MarkingAFileRepaintsItInTheColourMarksGet()
    {
        _app.Frame();

        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.Spacebar);
        _app.Frame();

        Assert.True(_app.Panels.Left.Marks.Contains("alpha.txt"));
        Assert.Equal(Skin.Lively.Marked.Ansi, _app.StyleOf("alpha.txt"));
    }

    [Fact]
    public void MovingTheCursorLeavesTheRestOfTheScreenAlone()
    {
        var before = _app.FrameLines();

        _app.Press(ConsoleKey.DownArrow);

        var after = _app.FrameLines();
        var moved = before.Zip(after).Count(static rows => rows.First != rows.Second);

        Assert.InRange(moved, 0, 4);
        Assert.Equal(before.Length, after.Length);
    }

    [Fact]
    public void EnteringAFolderTakesThePanelIntoIt()
    {
        _app.Frame();

        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.Enter);
        _app.Settled();

        var screen = _app.Frame();

        Assert.EndsWith("nested", _app.Panels.Left.Folder, StringComparison.Ordinal);
        Assert.Contains("nested", screen, StringComparison.Ordinal);
    }

    [Fact]
    public void TabMovesTheWorkToTheOtherPanel()
    {
        _app.Frame();

        Assert.False(_app.Panels.RightIsActive.Value);

        _app.Press(ConsoleKey.Tab);
        _app.Frame();

        Assert.True(_app.Panels.RightIsActive.Value);
    }

    /// <summary>
    /// A tab is a pair of panels of its own. Opening one leaves the pair already on screen alone, and
    /// switching back shows them as they were.
    /// </summary>
    [Fact]
    public void EachTabHasPanelsOfItsOwn()
    {
        var nested = Directory.CreateDirectory(Path.Combine(_app.Folder, "nested")).FullName;
        var here = _app.Panels.Left.Folder;

        _app.Panels.Add();
        _app.Settled();

        Assert.Equal(2, _app.Panels.Sessions.Count);
        Assert.Equal(1, _app.Panels.Open.Value);

        _app.Panels.Left.GoTo(nested);
        _app.Panels.Moved();
        _app.Settled();

        Assert.Equal(nested, _app.Panels.Left.Folder);

        _app.Panels.Show(0);
        _app.Settled();

        Assert.Equal(here, _app.Panels.Left.Folder);
    }

    /// <summary>
    /// Connecting lands in the panel that asked for it rather than in a tab of its own, and the tab
    /// says so: the side that went to the server is named after it.
    /// </summary>
    [Fact]
    public void ConnectingStaysInTheTabItWasAskedFrom()
    {
        _app.Panels.Right.Connect(new LocalSource(), _app.Folder);
        _app.Panels.Moved();
        _app.Settled();

        Assert.Single(_app.Panels.Sessions);
        Assert.Equal("local ⇄ local", _app.Panels.Current.Label);
    }

    /// <summary>Clicking a tab shows it, which is the one thing here a mouse does better than a key.</summary>
    [Fact]
    public void ClickingATabShowsIt()
    {
        _app.Panels.Add();
        _app.Settled();

        var lines = _app.FrameLines();
        var row = Array.FindIndex(lines, static line => line.Contains("local ⇄", StringComparison.Ordinal));

        Assert.True(row >= 0);

        _app.Click(row, lines[row].IndexOf("local", StringComparison.Ordinal));
        _app.Frame();

        Assert.Equal(0, _app.Panels.Open.Value);
    }

    /// <summary>The last tab stays: an application with no panels is not a state worth reaching.</summary>
    [Fact]
    public void TheLastTabWillNotClose()
    {
        Assert.False(_app.Panels.Close(_app.Panels.Current));
        Assert.Single(_app.Panels.Sessions);
    }

    [Fact]
    public void WhatIsTypedOnThePromptIsShownThere()
    {
        _app.Frame();
        _app.Type("echo hello");

        Assert.Contains("echo hello", _app.Frame(), StringComparison.Ordinal);
    }

    [Fact]
    public void RubbingOutTakesTheLastLetterOffThePrompt()
    {
        _app.Frame();
        _app.Type("echo hello");
        _app.Press(ConsoleKey.Backspace);

        var screen = _app.Frame();

        Assert.Contains("echo hell", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("echo hello", screen, StringComparison.Ordinal);
    }

    /// <summary>
    /// The bar along the bottom says what the keys of this moment do, in words. It is not a fixed row
    /// of ten: what is on it is what makes sense for what the cursor is on, spelled out rather than
    /// abbreviated to fit.
    /// </summary>
    [Fact]
    public void TheActionsAlongTheBottomAreSpelledOut()
    {
        var bar = _app.FrameLines()[^1];

        Assert.Contains("F3", bar, StringComparison.Ordinal);
        Assert.Contains("View", bar, StringComparison.Ordinal);
        Assert.Contains("Copy", bar, StringComparison.Ordinal);
        Assert.Contains("Delete", bar, StringComparison.Ordinal);
    }

    /// <summary>
    /// The column heads are the first thing anybody with a mouse clicks, so clicking one sorts by it.
    /// Clicking the same one again turns the order around, which is what the arrow beside it says.
    /// </summary>
    [Fact]
    public void ClickingAColumnHeadSortsByIt()
    {
        var lines = _app.FrameLines();
        var heads = Array.FindIndex(lines, static line => line.Contains("NAME", StringComparison.Ordinal));

        Assert.True(heads > 0);

        var size = lines[heads].IndexOf("SIZE", StringComparison.Ordinal);

        _app.Click(heads, size);
        _app.Frame();

        Assert.Equal(Sorting.Size, _app.Panels.Left.Sorting);

        _app.Click(heads, size);
        _app.Frame();

        Assert.True(_app.Panels.Left.Descending);
    }

    /// <summary>
    /// Marking changes every verb on the bar: with something in hand the question is no longer what
    /// this row is but what happens to what was marked.
    /// </summary>
    [Fact]
    public void MarkingSomethingRewritesTheBar()
    {
        using var wide = new ScreenApp(ViewKind.Commander, 150, 24);

        wide.Panels.Start(_app.Folder, _app.Folder);
        wide.Settled();
        wide.Press(ConsoleKey.DownArrow);
        wide.Press(ConsoleKey.Spacebar);

        var bar = wide.FrameLines()[^1];

        Assert.Contains("Copy 1 item", bar, StringComparison.Ordinal);
        Assert.Contains("Delete 1 item", bar, StringComparison.Ordinal);
    }

    /// <summary>
    /// A folder that cannot be read is an exception on the way up from the disk. What has to reach the
    /// screen is a sentence, not a stack trace and not an empty panel that looks like an empty folder.
    /// </summary>
    [Fact]
    public void AFolderThatCannotBeReadIsSaidOnThePanel()
    {
        var gone = Path.Combine(_app.Folder, "gone");

        Directory.CreateDirectory(gone);
        _app.Panels.Left.GoTo(gone);

        Assert.True(_app.Until(() => _app.Panels.Left.Folder == gone));

        Directory.Delete(gone);
        _app.Panels.Moved();
        _app.Settled();

        var screen = _app.Frame();

        Assert.Contains("gone", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("Unhandled", screen, StringComparison.Ordinal);
    }

    [Fact]
    public void ANarrowTerminalIsToldRatherThanDrawnInto()
    {
        using var narrow = new ScreenApp(ViewKind.Commander, 40, 10);

        Assert.Contains("too small", narrow.Frame(), StringComparison.OrdinalIgnoreCase);
    }

    private static int Occurrences(string screen, string name)
    {
        var found = 0;
        var at = screen.IndexOf(name, StringComparison.Ordinal);

        while (at >= 0)
        {
            found++;
            at = screen.IndexOf(name, at + name.Length, StringComparison.Ordinal);
        }

        return found;
    }
}
