using System;
using Arlecchino.Commander.Tests.Support;
using Arlecchino.Commander.Views;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Diagnostics;
using Xunit;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
///     The foot of the screen: the bar of keys, which wraps rather than losing what does not fit, and the
///     cards in the corner that say how work went.
/// </summary>
public sealed class CommanderFooterTests : IDisposable
{
    /// <summary>
    ///     The narrowest terminal the application will draw in rather than ask for more room, which is
    ///     narrow enough that the ten labels on the bar of keys no longer fit on one row.
    /// </summary>
    private const int Narrowest = 100;

    private readonly ScreenApp _app = Started.Showing();

    public void Dispose()
    {
        _app.Dispose();
    }

    /// <summary>
    ///     The bar along the bottom says what the keys of this moment do, in words spelled out rather than
    ///     abbreviated to fit.
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
    ///     dropping them. What would have gone is the menu and the way out of the application.
    /// </summary>
    [Fact]
    public void ANarrowBarWrapsRatherThanLosingTheLastKeys()
    {
        using var narrow = Started.Tabbed(Narrowest, 1);

        var bar = narrow.BarLine();

        Assert.Equal(2, narrow.BarLines().Length);
        Assert.Contains("F1", bar, StringComparison.Ordinal);
        Assert.Contains("F9", bar, StringComparison.Ordinal);
        Assert.Contains("F10", bar, StringComparison.Ordinal);
        Assert.Contains("Quit", bar, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The row a wrapped bar takes is a row the panels gave up, not a row it drew over. The command line
    ///     comes up by one, and it is the panels that end a line shorter.
    /// </summary>
    [Fact]
    public void TheRowAWrappedBarTakesComesOffThePanels()
    {
        using var wide = Started.Tabbed(150, 1);
        using var narrow = Started.Tabbed(Narrowest, 1);

        Assert.Single(wide.BarLines());
        Assert.Equal(2, narrow.BarLines().Length);
        Assert.Equal(wide.CommandLineRow() - 1, narrow.CommandLineRow());
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
}
