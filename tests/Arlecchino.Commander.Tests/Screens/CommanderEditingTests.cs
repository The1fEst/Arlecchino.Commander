using System;
using System.IO;
using Arlecchino.Commander.Tests.Support;
using Xunit;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
///     Everything this screen is typed into is edited alike: the field of an operation, the filter of a
///     list, and the search that runs while you type.
/// </summary>
public sealed class CommanderEditingTests : IDisposable
{
    private readonly ScreenApp _app = Started.Showing();

    public void Dispose()
    {
        _app.Dispose();
    }

    /// <summary>The field of an operation takes a paste where the caret is, not at the end of it.</summary>
    [Fact]
    public void PastedTextLandsInTheFieldOfAnOperation()
    {
        _app.Press(ConsoleKey.F7);
        _app.Frame();
        _app.Type("ab");
        _app.Press(ConsoleKey.LeftArrow);
        _app.ReadFromTerminal("\e[200~xy\e[201~");
        _app.Press(ConsoleKey.Enter);
        _app.Settled();

        Assert.True(Directory.Exists(Path.Combine(_app.Folder, "axyb")));
    }

    [Fact]
    public void TheCaretWalksTheFieldOfAnOperation()
    {
        _app.Press(ConsoleKey.F7);
        _app.Frame();
        _app.Type("ac");
        _app.Press(ConsoleKey.LeftArrow);
        _app.Type("b");
        _app.Press(ConsoleKey.Enter);
        _app.Settled();

        Assert.True(Directory.Exists(Path.Combine(_app.Folder, "abc")));
    }

    [Fact]
    public void SelectingInTheFieldAndTypingReplacesWhatWasSelected()
    {
        _app.Press(ConsoleKey.F7);
        _app.Frame();
        _app.Type("wrong");
        _app.Press(ConsoleKey.Home, shift: true);
        _app.Type("right");
        _app.Press(ConsoleKey.Enter);
        _app.Settled();

        Assert.True(Directory.Exists(Path.Combine(_app.Folder, "right")));
        Assert.False(Directory.Exists(Path.Combine(_app.Folder, "wrong")));
    }

    [Fact]
    public void CopyingTheFieldOfAnOperationReachesTheClipboard()
    {
        _app.Press(ConsoleKey.F7);
        _app.Frame();
        _app.Type("carried");
        _app.Press(ConsoleKey.Insert, control: true);
        _app.Frame();

        Assert.Equal("carried", _app.CopiedText);
    }

    /// <summary>
    ///     The filter of a list is a line of text as well: the caret walks it once something is typed, and
    ///     what is on it can be copied.
    /// </summary>
    [Fact]
    public void TheFilterOfAListIsEditedLikeAField()
    {
        _app.Sessions.Add();
        _app.Press(ConsoleKey.F2);
        _app.Frame();

        _app.Type("ac");
        _app.Press(ConsoleKey.LeftArrow);
        _app.Type("b");
        _app.Press(ConsoleKey.Insert, control: true);
        _app.Frame();

        Assert.Equal("abc", _app.CopiedText);
    }

    /// <summary>The search that runs while you type takes the caret keys once something is spelled.</summary>
    [Fact]
    public void TheSearchOnAPanelIsEditedLikeAField()
    {
        _app.Press(ConsoleKey.Oem2);
        _app.Type("apha");
        _app.Press(ConsoleKey.LeftArrow);
        _app.Press(ConsoleKey.LeftArrow);
        _app.Press(ConsoleKey.LeftArrow);
        _app.Type("l");

        Assert.Contains("alpha", _app.Frame(), StringComparison.Ordinal);
    }

    [Fact]
    public void CopyingWhatIsBeingSearchedForReachesTheClipboard()
    {
        _app.Press(ConsoleKey.Oem2);
        _app.Type("alp");
        _app.Press(ConsoleKey.Insert, control: true);
        _app.Frame();

        Assert.Equal("alp", _app.CopiedText);
    }
}
