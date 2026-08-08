using System;
using System.IO;
using System.Linq;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Tests.Support;
using Arlecchino.Commander.Views;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Diagnostics;
using Xunit;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
///     The panels as they reach the screen. Everything else here is about parsing what a server or a disk
///     said; this is about what the person in front of the terminal ends up looking at, which until now
///     nothing asserted.
/// </summary>
public sealed class CommanderScreenTests : IDisposable
{
    /// <summary>
    ///     Narrow enough that a fifth tab stops fitting even shortened, and the strip has to scroll. Four
    ///     still fit: the frame around the application spends a cell a side, so the band has the room.
    /// </summary>
    private const int Cramped = 125;

    /// <summary>
    ///     The narrowest terminal the application will draw in rather than ask for more room, which is
    ///     narrow enough that the ten labels on the bar of keys no longer fit on one row.
    /// </summary>
    private const int Narrowest = 100;

    private readonly ScreenApp _app = new(ViewKind.Commander);

    public CommanderScreenTests()
    {
        _app.Write("alpha.txt", "one");
        _app.Write("beta.txt", "two");
        Directory.CreateDirectory(Path.Combine(_app.Folder, "nested"));

        _app.Sessions.Start(_app.Folder, _app.Folder);
        _app.Settled();
    }

    public void Dispose()
    {
        _app.Dispose();
    }

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
    ///     The one thing a frame read as text cannot answer. A folder and a file read the same; what tells
    ///     them apart is the color, and the color is on the screen rather than in the words.
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

        Assert.True(_app.Sessions.Left.Marks.Contains("alpha.txt"));
        Assert.Equal(Skin.Lively.Marked.Ansi, _app.StyleOf("alpha.txt"));
    }

    /// <summary>
    ///     Copying with nothing marked takes what the cursor is on, which is the rule every other
    ///     operation on this screen already follows.
    /// </summary>
    [Fact]
    public void CopyingPathsWithNothingMarkedTakesTheOneUnderTheCursor()
    {
        _app.Frame();

        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.X);
        _app.Press(ConsoleKey.Y);
        _app.Frame();

        Assert.Equal(Path.Combine(_app.Folder, "alpha.txt"), _app.Copied);
    }

    /// <summary>
    ///     Marked files go over whole and one to a line, so what lands on the clipboard can be pasted into
    ///     a shell or an editor without anybody unpicking a separator first.
    /// </summary>
    [Fact]
    public void CopyingPathsTakesEveryMarkedFileOnItsOwnLine()
    {
        _app.Frame();

        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.Spacebar);
        _app.Press(ConsoleKey.Spacebar);
        _app.Press(ConsoleKey.X);
        _app.Press(ConsoleKey.Y);
        _app.Frame();

        Assert.Equal(
            $"{Path.Combine(_app.Folder, "alpha.txt")}\n{Path.Combine(_app.Folder, "beta.txt")}",
            _app.Copied);
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

        Assert.EndsWith("nested", _app.Sessions.Left.Folder, StringComparison.Ordinal);
        Assert.Contains("nested", screen, StringComparison.Ordinal);
    }

    [Fact]
    public void TabMovesTheWorkToTheOtherPanel()
    {
        _app.Frame();

        Assert.False(_app.Sessions.RightIsActive.Value);

        _app.Press(ConsoleKey.Tab);
        _app.Frame();

        Assert.True(_app.Sessions.RightIsActive.Value);
    }

    /// <summary>
    ///     A tab is a pair of panels of its own. Opening one leaves the pair already on screen alone, and
    ///     switching back shows them as they were.
    /// </summary>
    [Fact]
    public void EachTabHasPanelsOfItsOwn()
    {
        var nested = Directory.CreateDirectory(Path.Combine(_app.Folder, "nested")).FullName;
        var here = _app.Sessions.Left.Folder;

        _app.Sessions.Add();
        _app.Settled();

        Assert.Equal(2, _app.Sessions.All.Count);
        Assert.Equal(1, _app.Sessions.Open.Value);

        _app.Sessions.Left.GoTo(nested);
        _app.Sessions.Moved();
        _app.Settled();

        Assert.Equal(nested, _app.Sessions.Left.Folder);

        _app.Sessions.Show(0);
        _app.Settled();

        Assert.Equal(here, _app.Sessions.Left.Folder);
    }

    /// <summary>
    ///     Connecting lands in the panel that asked for it rather than in a tab of its own, and the tab
    ///     says so: the side that went to the server is named after it.
    /// </summary>
    [Fact]
    public void ConnectingStaysInTheTabItWasAskedFrom()
    {
        _app.Sessions.Right.Connect(new LocalSource(), _app.Folder);
        _app.Sessions.Moved();
        _app.Settled();

        Assert.Single(_app.Sessions.All);
        Assert.Equal("local ⇄ local", _app.Sessions.Current.Label);
    }

    /// <summary>Clicking a tab shows it, which is the one thing here a mouse does better than a key.</summary>
    [Fact]
    public void ClickingATabShowsIt()
    {
        _app.Sessions.Add();
        _app.Settled();

        var lines = _app.FrameLines();
        var row = Array.FindIndex(lines, static line => line.Contains("local ⇄", StringComparison.Ordinal));

        Assert.True(row >= 0);

        _app.Click(row, lines[row].IndexOf("local", StringComparison.Ordinal));
        _app.Frame();

        Assert.Equal(0, _app.Sessions.Open.Value);
    }

    /// <summary>
    ///     Clicking the <c>+</c> at the end of the tabs opens one. It is three cells wide, so it is the one
    ///     place on the band where the hit-testing has to agree with the drawing exactly.
    /// </summary>
    [Fact]
    public void ClickingThePlusOpensATab()
    {
        var lines = _app.FrameLines();
        var row = Array.FindIndex(lines, static line => line.Contains("local ⇄", StringComparison.Ordinal));

        Assert.True(row >= 0);

        _app.Click(row, lines[row].IndexOf('+'));
        _app.Settled();

        Assert.Equal(2, _app.Sessions.All.Count);
    }

    /// <summary>The cross on a tab closes it.</summary>
    [Fact]
    public void ClickingTheCrossClosesThatTab()
    {
        _app.Sessions.Add();
        _app.Settled();

        var lines = _app.FrameLines();
        var row = Array.FindIndex(lines, static line => line.Contains("local ⇄", StringComparison.Ordinal));

        Assert.True(row >= 0);

        _app.Click(row, lines[row].IndexOf('×'));
        _app.Settled();

        Assert.Single(_app.Sessions.All);
    }

    /// <summary>
    ///     The cross belongs to itself and not to the tab it sits on. The two hit areas touch, so a cross
    ///     that grew a cell or a tab that did would leave one of them unreachable — which is exactly what
    ///     used to be wrong with the plus at the end.
    /// </summary>
    [Fact]
    public void ClickingATabBesideItsCrossStillShowsIt()
    {
        _app.Sessions.Add();
        _app.Settled();

        var lines = _app.FrameLines();
        var row = Array.FindIndex(lines, static line => line.Contains("local ⇄", StringComparison.Ordinal));
        var cross = lines[row].IndexOf('×');

        _app.Click(row, cross - 2);
        _app.Settled();

        Assert.Equal(2, _app.Sessions.All.Count);
        Assert.Equal(0, _app.Sessions.Open.Value);
    }

    /// <summary>
    ///     The dot says which panel a tab was left in, and it says it for the tabs not on screen too. The
    ///     store holds that side only for the tab being worked in, so reading the store for all of them
    ///     put the dot on the left of every tab in the band whatever side it was really left on.
    /// </summary>
    [Fact]
    public void TheDotSaysWhichSideATabWasLeftIn()
    {
        _app.Press(ConsoleKey.Tab);
        _app.Press(ConsoleKey.T);
        _app.Press(ConsoleKey.K);
        _app.Settled();

        var band = _app.BandLine();

        Assert.Contains("local ⇄ ● local", band, StringComparison.Ordinal);
        Assert.Contains("● local ⇄ local", band, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Too many tabs for the band shortens their names rather than dropping the ones that no longer
    ///     fit. A tab that is not drawn cannot be clicked, and the one that would go is not the one anybody
    ///     would have chosen.
    /// </summary>
    [Fact]
    public void TooManyTabsShortenTheirNamesRatherThanDisappear()
    {
        using var narrow = Tabbed(130, 4);

        var band = narrow.BandLine();

        Assert.Equal(4, band.Count(letter => letter == '×'));
        Assert.Contains('…', band);
        Assert.DoesNotContain("local ⇄ local", band, StringComparison.Ordinal);
        Assert.DoesNotContain('‹', band);
    }

    /// <summary>
    ///     Past the point where shortening leaves a name saying anything, the strip scrolls instead. The
    ///     tab being worked in is the one that must stay in view — a strip that has scrolled away from the
    ///     panels on screen says nothing about where the work is.
    ///
    ///     Five tabs at the narrowest width rather than four: the frame around the application spends a
    ///     cell a side, not two, so four now fit shortened, and it takes a fifth to make the strip scroll.
    /// </summary>
    [Fact]
    public void PastShorteningTheStripScrollsAndKeepsTheOpenTabInView()
    {
        using var narrow = Tabbed(Cramped, 5);

        var band = narrow.BandLine();
        var showing = band.Count(letter => letter == '×');

        Assert.True(showing < 5, band);
        Assert.Contains('‹', band);
        Assert.Contains('›', band);
        Assert.Equal(4, narrow.Sessions.Open.Value);

        narrow.Click(narrow.Inset, band.IndexOf('●'));
        narrow.Settled();

        Assert.Equal(5 - showing, narrow.Sessions.Open.Value);
    }

    /// <summary>
    ///     Clicking a marker scrolls to the tabs that did not fit: a click steps the strip one tab, and the
    ///     tab that was off the left edge is then a tab that can be clicked.
    ///
    ///     Five tabs and not four, and the marker asserted for before it is clicked: four fit at this width,
    ///     so this test used to click a marker that was not on the band, which clicks nothing and proves
    ///     nothing.
    /// </summary>
    [Fact]
    public void ClickingTheMarkerScrollsToTheTabsBehindIt()
    {
        using var narrow = Tabbed(Cramped, 5);

        Assert.Contains('‹', narrow.BandLine());

        narrow.Click(narrow.Inset, narrow.BandLine().IndexOf('‹'));
        narrow.Settled();

        narrow.Click(narrow.Inset, narrow.BandLine().IndexOf('●'));
        narrow.Settled();

        Assert.Equal(1, narrow.Sessions.Open.Value);
    }

    /// <summary>An application of a given width with a given number of tabs open, settled and drawn.</summary>
    /// <param name="width">How wide the terminal is.</param>
    /// <param name="tabs">How many tabs to open.</param>
    /// <returns>The application, for the test to dispose.</returns>
    private static ScreenApp Tabbed(int width, int tabs)
    {
        var app = new ScreenApp(ViewKind.Commander, width);

        app.Settled();

        for (var opened = 1; opened < tabs; opened++)
        {
            app.Sessions.Add();
        }

        app.Settled();

        return app;
    }

    /// <summary>With one tab open there is no cross: the last one does not close.</summary>
    [Fact]
    public void TheOnlyTabWearsNoCross()
    {
        var lines = _app.FrameLines();
        var row = Array.FindIndex(lines, static line => line.Contains("local ⇄", StringComparison.Ordinal));

        Assert.True(row >= 0);
        Assert.DoesNotContain('×', lines[row]);
    }

    /// <summary>A tab can be opened, stepped between and closed without touching the mouse.</summary>
    [Fact]
    public void TabsAreWorkedFromTheKeyboard()
    {
        _app.Press(ConsoleKey.T);
        _app.Press(ConsoleKey.K);
        _app.Settled();

        Assert.Equal(2, _app.Sessions.All.Count);
        Assert.Equal(1, _app.Sessions.Open.Value);

        _app.Press(ConsoleKey.T);
        _app.Press(ConsoleKey.H);
        _app.Frame();

        Assert.Equal(0, _app.Sessions.Open.Value);

        _app.Press(ConsoleKey.T);
        _app.Press(ConsoleKey.L);
        _app.Frame();

        Assert.Equal(1, _app.Sessions.Open.Value);

        _app.Press(ConsoleKey.T);
        _app.Press(ConsoleKey.J);
        _app.Settled();

        Assert.Single(_app.Sessions.All);
    }

    /// <summary>
    ///     <c>Ctrl+PageUp</c> goes to the folder above, the way it always has. It once meant the tab beside
    ///     this one as well, in code the router never reached — a view's commands are read before its
    ///     <c>Handle</c>, so the folder won every time and the tab could not be got to at all.
    /// </summary>
    [Fact]
    public void ControlPageUpGoesToTheFolderAbove()
    {
        var above = Directory.GetParent(_app.Folder)!.FullName;

        _app.Press(ConsoleKey.PageUp, control: true);
        _app.Settled();

        Assert.Equal(above, _app.Sessions.Left.Folder);
        Assert.Single(_app.Sessions.All);
    }

    /// <summary>
    ///     The button that says <c>Enter Make</c> makes the folder when it is clicked. A button that can
    ///     only be pressed by the key printed on it is a picture of a button.
    /// </summary>
    [Fact]
    public void ClickingTheConfirmButtonAnswersTheQuestion()
    {
        _app.Press(ConsoleKey.F7);
        _app.Frame();
        _app.Type("clicked");

        Click(_app.FrameLines(), "Enter ");
        _app.Settled();

        Assert.Null(_app.State.Modal);
        Assert.True(Directory.Exists(Path.Combine(_app.Folder, "clicked")));
    }

    /// <summary>And the one beside it closes the question without answering it.</summary>
    [Fact]
    public void ClickingTheCancelButtonCallsItOff()
    {
        _app.Press(ConsoleKey.F7);
        _app.Frame();
        _app.Type("never");

        Click(_app.FrameLines(), "Esc Cancel");
        _app.Settled();

        Assert.Null(_app.State.Modal);
        Assert.False(Directory.Exists(Path.Combine(_app.Folder, "never")));
    }

    /// <summary>
    ///     A row of a list is selected by a click and run by a second one on the row already selected,
    ///     which is the rule everywhere else a list is clicked.
    /// </summary>
    [Fact]
    public void ClickingARowTwiceRunsIt()
    {
        _app.Sessions.Add();
        _app.Press(ConsoleKey.F2);
        _app.Frame();

        Assert.Equal(1, _app.Sessions.Open.Value);

        Click(_app.FrameLines(), "1 · local");
        _app.Frame();
        Click(_app.FrameLines(), "1 · local");
        _app.Settled();

        Assert.Null(_app.State.Modal);
        Assert.Equal(0, _app.Sessions.Open.Value);
    }

    /// <summary>Clicking away changes nothing: a stray click must not discard what was typed.</summary>
    [Fact]
    public void ClickingOutsideADialogLeavesItOpen()
    {
        _app.Press(ConsoleKey.F7);
        _app.Frame();
        _app.Type("kept");

        _app.Click(0, 0);

        var screen = _app.Frame();

        Assert.NotNull(_app.State.Modal);
        Assert.Contains("kept", screen, StringComparison.Ordinal);
    }

    /// <summary>Clicks where some text is drawn, wherever the dialog happened to place it.</summary>
    /// <param name="lines">The frame as it was drawn.</param>
    /// <param name="text">What to click on.</param>
    private void Click(string[] lines, string text)
    {
        for (var row = 0; row < lines.Length; row++)
        {
            var at = lines[row].IndexOf(text, StringComparison.Ordinal);

            if (at < 0)
            {
                continue;
            }

            _app.Click(row, at);

            return;
        }

        Assert.Fail($"'{text}' is not on the screen.");
    }

    /// <summary>The last tab stays: an application with no panels is not a state worth reaching.</summary>
    [Fact]
    public void TheLastTabWillNotClose()
    {
        Assert.False(_app.Sessions.Close(_app.Sessions.Current));
        Assert.Single(_app.Sessions.All);
    }

    [Fact]
    public void WhatIsTypedOnThePromptIsShownThere()
    {
        _app.Frame();
        _app.Type(":echo hello");

        Assert.Contains("echo hello", _app.Frame(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     The row says whether it has the keyboard. It used to take letters whenever they were typed, so
    ///     there was nothing to say; now that it is asked for, a row that looks the same either way leaves
    ///     the keyboard somewhere the eye cannot find it. Asleep it names the key that wakes it instead.
    /// </summary>
    [Fact]
    public void ThePromptSaysWhetherItHasTheKeyboard()
    {
        var asleep = _app.FrameLines()[_app.CommandLineRow()];

        Assert.Contains("type a command here", asleep, StringComparison.Ordinal);

        _app.Type(":");

        var awake = _app.FrameLines()[_app.CommandLineRow()];

        Assert.DoesNotContain("type a command here", awake, StringComparison.Ordinal);
        Assert.Contains("everything the commands printed", awake, StringComparison.Ordinal);
    }

    [Fact]
    public void RubbingOutTakesTheLastLetterOffThePrompt()
    {
        _app.Frame();
        _app.Type(":echo hello");
        _app.Press(ConsoleKey.Backspace);

        var screen = _app.Frame();

        Assert.Contains("echo hell", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("echo hello", screen, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A paste is not a run of key presses — the terminal hands it over as a block of its own — so a
    ///     screen that only answers keys loses it. What was pasted goes on the line at the cursor, the same
    ///     place typing would have put it.
    /// </summary>
    [Fact]
    public void PastedTextLandsOnThePrompt()
    {
        _app.Frame();
        _app.Type(":echo ");
        _app.ReadFromTerminal("\e[200~hello\e[201~");

        Assert.Contains("echo hello", _app.Frame(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     A paste while the panel has the keyboard wakes the line and lands there. There is nowhere else on
    ///     this screen for text to go, and a paste that vanishes is the one thing worse than a paste that
    ///     asked first.
    /// </summary>
    [Fact]
    public void APasteWakesThePromptAndLandsOnIt()
    {
        _app.Frame();
        _app.ReadFromTerminal("\e[200~git status\e[201~");

        var line = _app.FrameLines()[_app.CommandLineRow()];

        Assert.Contains("git status", line, StringComparison.Ordinal);
        Assert.DoesNotContain("type a command here", line, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Only the first line of a paste reaches the row, and none of it is run. A clipboard carrying a
    ///     newline would otherwise press Enter on a command nobody has read yet.
    /// </summary>
    [Fact]
    public void OnlyTheFirstLineOfAPasteReachesThePrompt()
    {
        _app.Frame();
        _app.ReadFromTerminal("\e[200~echo one\r\nrm -rf two\e[201~");

        var line = _app.FrameLines()[_app.CommandLineRow()];

        Assert.Contains("echo one", line, StringComparison.Ordinal);
        Assert.DoesNotContain("rm -rf two", _app.Frame(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     While the search that runs as you type has the keyboard, a paste is part of what is being spelled
    ///     rather than the start of a command.
    /// </summary>
    [Fact]
    public void PastingIntoTheSearchSpellsTheNameOut()
    {
        _app.Settled();
        _app.Press(ConsoleKey.Oem2);
        _app.ReadFromTerminal("\e[200~bet\e[201~");

        var line = _app.FrameLines()[_app.CommandLineRow()];

        Assert.Contains("jump to  bet", _app.Frame(), StringComparison.Ordinal);
        Assert.Contains("type a command here", line, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The bar along the bottom says what the keys of this moment do, in words: spelled out rather than
    ///     abbreviated to fit, since a bar of ten abbreviations answers nothing anybody was asking.
    /// </summary>
    [Fact]
    public void TheActionsAlongTheBottomAreSpelledOut()
    {
        var bar = _app.BarLine();

        Assert.Contains("F3", bar, StringComparison.Ordinal);
        Assert.Contains("View", bar, StringComparison.Ordinal);
        Assert.Contains("Copy", bar, StringComparison.Ordinal);
        Assert.Contains("Delete", bar, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A terminal too narrow for ten spelled-out labels carries the rest onto another row rather than
    ///     dropping them. The keys that would have gone are the last on the bar, and the last on the bar are
    ///     the menu and the way out of the application.
    /// </summary>
    [Fact]
    public void ANarrowBarWrapsRatherThanLosingTheLastKeys()
    {
        using var narrow = Tabbed(Narrowest, 1);

        var bar = narrow.BarLine();

        Assert.Equal(2, narrow.BarLines().Length);
        Assert.Contains("F1", bar, StringComparison.Ordinal);
        Assert.Contains("F9", bar, StringComparison.Ordinal);
        Assert.Contains("F10", bar, StringComparison.Ordinal);
        Assert.Contains("Quit", bar, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The row a wrapped bar takes is a row the panels gave up, not a row it drew over: the command line
    ///     above it comes up by one, and it is the panels that end a line shorter. Nothing tells the screen
    ///     how tall the bar is — it finds out by drawing — so this is the whole of what keeps the two in
    ///     step.
    /// </summary>
    [Fact]
    public void TheRowAWrappedBarTakesComesOffThePanels()
    {
        using var wide = Tabbed(150, 1);
        using var narrow = Tabbed(Narrowest, 1);

        Assert.Single(wide.BarLines());
        Assert.Equal(2, narrow.BarLines().Length);
        Assert.Equal(wide.CommandLineRow() - 1, narrow.CommandLineRow());
    }

    /// <summary>
    ///     The column heads are the first thing anybody with a mouse clicks, so clicking one sorts by it.
    ///     Clicking the same one again turns the order around, which is what the arrow beside it says.
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

        Assert.Equal(Sorting.Size, _app.Sessions.Left.Sorting);

        _app.Click(heads, size);
        _app.Frame();

        Assert.True(_app.Sessions.Left.Descending);
    }

    /// <summary>
    ///     Marking changes every verb on the bar: with something in hand the question is no longer what
    ///     this row is but what happens to what was marked.
    /// </summary>
    [Fact]
    public void MarkingSomethingRewritesTheBar()
    {
        using var wide = new ScreenApp(ViewKind.Commander, 150, 24);

        wide.Sessions.Start(_app.Folder, _app.Folder);
        wide.Settled();
        wide.Press(ConsoleKey.DownArrow);
        wide.Press(ConsoleKey.Spacebar);

        var bar = wide.BarLine();

        Assert.Contains("Copy 1 item", bar, StringComparison.Ordinal);
        Assert.Contains("Delete 1 item", bar, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A folder that cannot be read is an exception on the way up from the disk. The screen has to be
    ///     given a sentence: not a stack trace, and not an empty panel that reads as an empty folder.
    /// </summary>
    [Fact]
    public void AFolderThatCannotBeReadIsSaidOnThePanel()
    {
        var gone = Path.Combine(_app.Folder, "gone");

        Directory.CreateDirectory(gone);
        _app.Sessions.Left.GoTo(gone);

        Assert.True(_app.Until(() => _app.Sessions.Left.Folder == gone));

        Directory.Delete(gone);
        _app.Sessions.Moved();
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

    /// <summary>
    ///     The card in the corner is the whole of the reporting: what a job came to is its color, and
    ///     what it says is the words the output row used to hold on its own.
    /// </summary>
    [Fact]
    public void WorkThatWentWellGetsACardInTheColourThingsThatWorkedGet()
    {
        _app.State.Output = "Reloaded";

        Assert.Contains("Reloaded", _app.Frame(), StringComparison.Ordinal);
        Assert.Equal(Skin.Overlay.Done.Ansi, _app.StyleOf("Reloaded"));
    }

    [Fact]
    public void WorkThatFailedSaysWhyAndOffersToShowTheRest()
    {
        _app.State.Notifications.Raise(new(DateTimeOffset.Now, NotificationLevel.Failure, "3 files would not copy")
        {
            Detail = static () => "one.txt: in use"
        });

        var frame = _app.Frame();

        Assert.Contains("3 files would not copy", frame, StringComparison.Ordinal);
        Assert.Contains("one.txt: in use", frame, StringComparison.Ordinal);
        Assert.Contains("Enter to read the rest", frame, StringComparison.Ordinal);
        Assert.Equal(Skin.Overlay.Warning.Ansi, _app.StyleOf("3 files would not copy"));
    }

    [Fact]
    public void TwoThingsSaidLatelyStackIntoTwoCards()
    {
        _app.State.Output = "Reloaded";
        _app.State.Output = "Hidden files shown";

        var lines = _app.FrameLines();
        var newest = Array.FindIndex(lines, line => line.Contains("Hidden files shown", StringComparison.Ordinal));
        var older = Array.FindIndex(lines, line => line.Contains("Reloaded", StringComparison.Ordinal));

        Assert.True(older >= 0, "the older card is still on screen");
        Assert.True(newest > older, "the newest card is the one nearest the command line");
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
