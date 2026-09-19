using System;
using System.IO;
using Arlecchino.Commander.Tests.Support;
using Arlecchino.Commander.Views;
using Xunit;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
///     A file as the viewer shows it. The view is built with an empty body and fills in when the read is
///     answered, so every frame here is waited for rather than assumed.
/// </summary>
public sealed class ViewerScreenTests : IDisposable
{
    private readonly ScreenApp _app = new(ViewKind.Viewer);

    public void Dispose()
    {
        _app.Dispose();
    }

    private void Viewing(string name, string text)
    {
        var path = _app.Write(name, text);

        _app.Sessions.Viewing.Value = path;
        _app.Sessions.ViewingSize = new FileInfo(path).Length;
    }

    [Fact]
    public void TheTextOfTheFileIsOnScreen()
    {
        Viewing("notes.txt", "first line\nsecond line\nthird line");

        Assert.True(_app.Shows("first line"));
        Assert.Contains("third line", _app.Frame(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     A picture says what it is, how large, and how it is being drawn. Half-blocks, sixel and the
    ///     kitty protocol differ by more than they look.
    /// </summary>
    [Fact]
    public void APictureSaysItsFormatItsSizeAndHowItIsDrawn()
    {
        var path = Path.Combine(_app.Folder, "dot.pnm");

        using (var file = File.Create(path))
        {
            file.Write("P6\n2 1\n255\n"u8);
            file.Write([255, 0, 0, 0, 255, 0]);
        }

        _app.Sessions.Viewing.Value = path;
        _app.Sessions.ViewingSize = new FileInfo(path).Length;

        Assert.True(_app.Shows("pnm, 2×1, drawn as blocks"));
    }

    /// <summary>
    ///     A document is drawn as what its marks say rather than as the marks. The hashes and the dashes
    ///     are the file saying how to read it, and a viewer that writes them out has not read it.
    /// </summary>
    [Fact]
    public void ADocumentIsDrawnAsWhatItsMarksSay()
    {
        Viewing("notes.md", "# Title\n\n- one\n- two\n");

        Assert.True(_app.Shows("• one"));
        Assert.DoesNotContain("# Title", _app.Frame(), StringComparison.Ordinal);
    }

    [Fact]
    public void ADocumentSaysThatIsWhatItIs()
    {
        Viewing("notes.md", "# Title\n");

        Assert.True(_app.Shows("markdown"));
    }

    /// <summary>
    ///     A picture a document names is looked for beside the document and drawn where it was named. The
    ///     terminal here speaks no graphics protocol, so what arrives is the half blocks it falls back to.
    /// </summary>
    [Fact]
    public void APictureADocumentNamesIsDrawnFromBesideIt()
    {
        _app.Write(
            "shape.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='40'>" +
            "<rect width='40' height='40' fill='#c9382b'/></svg>");

        Viewing("notes.md", "![the shape](shape.svg)\n");

        Assert.True(_app.Shows("▀"));
    }

    /// <summary>
    ///     A document written for a browser names pictures on the web as readily as ones beside it, and
    ///     fetching such a picture tells whoever wrote the document that the file had been opened.
    /// </summary>
    [Fact]
    public void APictureSomewhereOnTheWebIsNamedRatherThanFetched()
    {
        Viewing("notes.md", "![a badge](https://img.example/badge.svg)\n");

        Assert.True(_app.Shows("a badge"));
    }

    [Fact]
    public void AFileThatIsMerelyTextIsStillDrawnAsText()
    {
        Viewing("notes.txt", "# Title\n");

        Assert.True(_app.Shows("# Title"));
    }

    [Fact]
    public void TheNameOfTheFileIsInTheChrome()
    {
        Viewing("notes.txt", "anything");

        Assert.Contains("notes.txt", _app.Frame(), StringComparison.Ordinal);
    }

    [Fact]
    public void ALineWiderThanTheScreenDoesNotSpillOverIt()
    {
        Viewing("wide.txt", new('x', 400));

        Assert.True(_app.Shows("xxx"));

        var lines = _app.FrameLines();

        Assert.All(lines, row => Assert.True(row.Length <= _app.Width));
    }

    [Fact]
    public void AFileOfNothingStillDraws()
    {
        Viewing("empty.txt", "");

        Assert.Equal(30, _app.FrameLines().Length);
    }

    [Fact]
    public void EscapeGoesBackToThePanels()
    {
        Viewing("notes.txt", "anything");
        _app.Frame();

        _app.Press(ConsoleKey.Escape);

        Assert.Equal(ViewKind.Commander, _app.Navigator.CurrentRoute);
    }
}
