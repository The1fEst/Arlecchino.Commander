using System;
using System.IO;
using System.Linq;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Tests.Support;
using Xunit;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
///     The band of tabs along the top: what it says, what fits on it, and what happens when a tab, a cross
///     or a scroll marker is clicked.
/// </summary>
public sealed class CommanderTabTests : IDisposable
{
    /// <summary>
    ///     Narrow enough that a fifth tab stops fitting even shortened, and the strip has to scroll. Four
    ///     still fit: the frame around the application spends a cell a side, so the band has the room.
    /// </summary>
    private const int Cramped = 125;

    private readonly ScreenApp _app = Started.Showing();

    public void Dispose()
    {
        _app.Dispose();
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
        using var narrow = Started.Tabbed(130, 4);

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
        using var narrow = Started.Tabbed(Cramped, 5);

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
        using var narrow = Started.Tabbed(Cramped, 5);

        Assert.Contains('‹', narrow.BandLine());

        narrow.Click(narrow.Inset, narrow.BandLine().IndexOf('‹'));
        narrow.Settled();

        narrow.Click(narrow.Inset, narrow.BandLine().IndexOf('●'));
        narrow.Settled();

        Assert.Equal(1, narrow.Sessions.Open.Value);
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

    /// <summary>The last tab stays: an application with no panels is not a state worth reaching.</summary>
    [Fact]
    public void TheLastTabWillNotClose()
    {
        Assert.False(_app.Sessions.Close(_app.Sessions.Current));
        Assert.Single(_app.Sessions.All);
    }
}
