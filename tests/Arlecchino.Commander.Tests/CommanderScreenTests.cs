using System;
using System.IO;
using System.Linq;
using Arlecchino.Commander.Views;
using Arlecchino.Rendering;
using Xunit;

namespace Arlecchino.Commander.Tests;

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

        Assert.Equal(Theme.Accent.Ansi, _app.StyleOf("nested"));
        Assert.Equal(Theme.Default.Ansi, _app.StyleOf("alpha.txt"));
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
        Assert.Equal(Theme.Warning.Ansi, _app.StyleOf("alpha.txt"));
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

    [Fact]
    public void TheFunctionKeyBarIsAlongTheBottom()
    {
        var lines = _app.FrameLines();

        Assert.Contains("Quit", lines[^1], StringComparison.Ordinal);
        Assert.Contains("Help", lines[^1], StringComparison.Ordinal);
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
        Directory.Delete(gone);

        _app.Panels.Moved();

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
