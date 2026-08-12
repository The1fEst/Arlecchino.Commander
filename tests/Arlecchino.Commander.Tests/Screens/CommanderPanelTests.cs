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
    ///     <c>Ctrl+PageUp</c> goes to the folder above and nowhere else. A view's commands are read before
    ///     its <c>Handle</c>, so a second meaning bound here could never be reached.
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
