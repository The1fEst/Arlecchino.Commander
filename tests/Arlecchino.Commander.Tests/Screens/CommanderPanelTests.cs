using System;
using System.IO;
using System.Linq;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Tests.Support;
using Arlecchino.Commander.Views;
using Arlecchino.Commander.Widgets.Chrome;
using Xunit;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
///     The panels as they reach the screen. Everything else here is about parsing what a server or a disk
///     said; this is about what the person in front of the terminal ends up looking at, which until now
///     nothing asserted.
/// </summary>
public sealed class CommanderPanelTests : IDisposable
{
    private readonly ScreenApp _app = Started.Showing();

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
        Assert.Equal(Skin.Lively.Hint.Ansi, _app.StyleOf("txt"));
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
        Assert.Equal(Skin.Lively.MarkName.Ansi, _app.StyleOf("alpha.txt"));
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
        _app.Press(ConsoleKey.C);
        _app.Press(ConsoleKey.C);
        _app.Frame();

        Assert.Equal(Path.Combine(_app.Folder, "alpha.txt"), _app.CopiedText);
    }

    /// <summary>
    ///     Marked files go over whole and one to a line, so what lands on the clipboard can be pasted into
    ///     a shell or an editor with no separator to unpick first.
    /// </summary>
    [Fact]
    public void CopyingPathsTakesEveryMarkedFileOnItsOwnLine()
    {
        _app.Frame();

        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.Spacebar);
        _app.Press(ConsoleKey.Spacebar);
        _app.Press(ConsoleKey.C);
        _app.Press(ConsoleKey.C);
        _app.Frame();

        Assert.Equal(
            $"{Path.Combine(_app.Folder, "alpha.txt")}\n{Path.Combine(_app.Folder, "beta.txt")}",
            _app.CopiedText);
    }

    /// <summary>
    ///     The name goes over as the file has it, extension and all, rather than as the column had room to
    ///     draw it.
    /// </summary>
    [Fact]
    public void CopyingNamesTakesTheNameAndNotThePath()
    {
        _app.Frame();

        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.C);
        _app.Press(ConsoleKey.F);
        _app.Frame();

        Assert.Equal("alpha.txt", _app.CopiedText);
    }

    /// <summary>The extension is cut off the name, which is what a rename is usually reaching for.</summary>
    [Fact]
    public void CopyingBareNamesCutsTheExtensionOff()
    {
        _app.Frame();

        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.C);
        _app.Press(ConsoleKey.N);
        _app.Frame();

        Assert.Equal("alpha", _app.CopiedText);
    }

    /// <summary>
    ///     A folder has no extension to cut, so its name goes over whole. Cutting at the last dot would make
    ///     a folder called <c>2026.08</c> into <c>2026</c>.
    /// </summary>
    [Fact]
    public void CopyingABareNameLeavesAFolderWhole()
    {
        Directory.CreateDirectory(Path.Combine(_app.Folder, "2026.08"));

        _app.Press(ConsoleKey.X);
        _app.Press(ConsoleKey.R);
        _app.Settled();

        _app.Press(ConsoleKey.Home);
        _app.Press(ConsoleKey.DownArrow);
        _app.Frame();
        _app.Press(ConsoleKey.C);
        _app.Press(ConsoleKey.N);
        _app.Frame();

        Assert.Equal("2026.08", _app.CopiedText);
    }

    /// <summary>
    ///     The folder is what the panel is looking at rather than what is picked out inside it, so the marks
    ///     make no difference to it.
    /// </summary>
    [Fact]
    public void CopyingTheFolderTakesWhereThePanelIsLooking()
    {
        _app.Frame();

        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.Spacebar);
        _app.Press(ConsoleKey.C);
        _app.Press(ConsoleKey.D);
        _app.Frame();

        Assert.Equal(_app.Folder, _app.CopiedText);
    }

    [Fact]
    public void MovingTheCursorLeavesTheRestOfTheScreenAlone()
    {
        var first = _app.FrameLines();

        _app.Press(ConsoleKey.DownArrow);

        var second = _app.FrameLines();
        var differences = first.Zip(second).Count(static rows => rows.First != rows.Second);

        Assert.InRange(differences, 0, 4);
        Assert.Equal(first.Length, second.Length);
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
    ///     <c>Ctrl+PageUp</c> goes to the folder above and nowhere else. A view's commands are read before
    ///     its <c>Handle</c>, so a second meaning bound here could never be reached.
    /// </summary>
    [Fact]
    public void ControlPageUpGoesToTheFolderAbove()
    {
        var parent = Directory.GetParent(_app.Folder)!.FullName;

        _app.Press(ConsoleKey.PageUp, control: true);
        _app.Settled();

        Assert.Equal(parent, _app.Sessions.Left.Folder);
        Assert.Single(_app.Sessions.All);
    }

    /// <summary>
    ///     Clicking a column head sorts by it. Clicking the same one again turns the order around, which is
    ///     what the arrow beside it says.
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
    ///     A folder that cannot be read is an exception on the way up from the disk. The screen has to be
    ///     given a sentence: not a stack trace, and not an empty panel that reads as an empty folder.
    /// </summary>
    [Fact]
    public void AFolderThatCannotBeReadIsSaidOnThePanel()
    {
        var folder = Path.Combine(_app.Folder, "gone");

        Directory.CreateDirectory(folder);
        _app.Sessions.Left.GoTo(folder);

        Assert.True(_app.Until(() => _app.Sessions.Left.Folder == folder));

        Directory.Delete(folder);
        _app.Sessions.Moved();
        _app.Settled();

        var screen = _app.Frame();

        Assert.Contains("gone", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("Unhandled", screen, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A file made by something else — another window, a build, a download — turns up in the panel with
    ///     no key pressed. This is the whole point of the watching, and the disk is what says so.
    /// </summary>
    [Fact]
    public void AFileMadeOutsideTheApplicationTurnsUpByItself()
    {
        Assert.DoesNotContain("gamma.txt", _app.Frame(), StringComparison.Ordinal);

        File.WriteAllText(Path.Combine(_app.Folder, "gamma.txt"), "three");

        Assert.True(_app.Shows("gamma.txt"));
    }

    /// <summary>A file that went away the same way stops being drawn, and the panel says so quietly.</summary>
    [Fact]
    public void AFileDeletedOutsideTheApplicationGoesByItself()
    {
        Assert.Contains("beta.txt", _app.Frame(), StringComparison.Ordinal);

        File.Delete(Path.Combine(_app.Folder, "beta.txt"));

        Assert.True(_app.Until(() => !_app.Frame().Contains("beta.txt", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     A reading the watch asked for is said nothing about: the panel keeps its cursor, its marks and
    ///     its names, and never flashes the word it shows while a folder is being waited for.
    /// </summary>
    [Fact]
    public void WhatTheWatchReadsArrivesWithoutTheWordForWaiting()
    {
        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.Spacebar);
        _app.Frame();

        File.WriteAllText(Path.Combine(_app.Folder, "gamma.txt"), "three");

        Assert.True(_app.Shows("gamma.txt"));
        Assert.DoesNotContain("reading", _app.Frame(), StringComparison.Ordinal);
        Assert.True(_app.Sessions.Left.Marks.Contains("alpha.txt"));
    }

    [Fact]
    public void ANarrowTerminalIsToldRatherThanDrawnInto()
    {
        using var narrow = new ScreenApp(ViewKind.Commander, 40, 10);

        Assert.Contains("too small", narrow.Frame(), StringComparison.OrdinalIgnoreCase);
    }

    private static int Occurrences(string screen, string name)
    {
        var count = 0;
        var at = screen.IndexOf(name, StringComparison.Ordinal);

        while (at >= 0)
        {
            count++;
            at = screen.IndexOf(name, at + name.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
