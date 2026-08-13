using System;
using System.IO;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Tests.Support;
using Xunit;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
///     The settings line: the key that asks for it, the box that narrows as it is typed into, and what
///     becomes of a setting once it has been kept.
/// </summary>
public sealed class CommanderSettingsTests : IDisposable
{
    private readonly ScreenApp _app = Started.Showing();

    public void Dispose() => _app.Dispose();

    /// <summary>
    ///     The exclamation mark asks for the line, as the colon asks for the command line. Until then the
    ///     row belongs to the command line, and the panel has the alphabet.
    /// </summary>
    [Fact]
    public void TheExclamationMarkOpensTheSettingsLine()
    {
        Assert.DoesNotContain("Settings", _app.Frame(), StringComparison.Ordinal);

        _app.Type("!");

        var line = _app.FrameLines()[_app.CommandLineRow()];

        Assert.Contains("Settings", line, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The box over the line lists what could be typed next, with what each of them is for. Opened on
    ///     nothing typed it lists everything there is, which is how a setting is found without knowing it
    ///     was there.
    /// </summary>
    [Fact]
    public void TheBoxListsWhatCanBeSet()
    {
        _app.Type("!");

        var screen = _app.Frame();

        Assert.Contains(Settings.EditorName, screen, StringComparison.Ordinal);
        Assert.Contains("The program F4 opens a file in", screen, StringComparison.Ordinal);
    }

    /// <summary>A name nothing answers to leaves the line alone and says as much.</summary>
    [Fact]
    public void ANameNothingAnswersToIsSaidSo()
    {
        _app.Type("!nonesuch vim");
        _app.Press(ConsoleKey.Enter);

        Assert.Contains("No setting called nonesuch", _app.Frame(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     A name and a value together keep the setting, and keeping it writes the file — a file manager is
    ///     quit by closing the terminal as often as by pressing the key for it.
    /// </summary>
    [Fact]
    public void SettingTheEditorKeepsItAndWritesIt()
    {
        _app.Type("!editor micro");
        _app.Press(ConsoleKey.Enter);
        _app.Frame();

        Assert.Equal("micro", _app.Settings.Editor);
        Assert.Contains("micro", File.ReadAllText(_app.Settings.Place), StringComparison.Ordinal);
        Assert.StartsWith(_app.Config, _app.Settings.Place, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A name on its own fills in what that setting is now, so the value can be looked at and edited
    ///     rather than typed again from nothing.
    /// </summary>
    [Fact]
    public void ANameOnItsOwnFillsInWhatItIsNow()
    {
        _app.Type("!editor nano");
        _app.Press(ConsoleKey.Enter);
        _app.Frame();
        _app.Type("!editor");
        _app.Press(ConsoleKey.Enter);

        Assert.Contains("editor nano", _app.FrameLines()[_app.CommandLineRow()], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Escape gives the row back to the command line, which holds it whenever nothing else has asked.
    /// </summary>
    [Fact]
    public void EscapeGivesTheRowBack()
    {
        _app.Type("!edit");
        _app.Press(ConsoleKey.Escape);

        var line = _app.FrameLines()[_app.CommandLineRow()];

        Assert.DoesNotContain("Settings", line, StringComparison.Ordinal);
        Assert.Contains("type a command here", line, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The watching is set in seconds, and the word for none turns it off altogether. It is read back as
    ///     a length of time rather than as what was typed, since that is what the panels are handed.
    /// </summary>
    [Fact]
    public void TheWatchingIsSetInSecondsAndTurnedOffByAWord()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), _app.Settings.Watch);

        _app.Type($"!{Settings.WatchName} 10");
        _app.Press(ConsoleKey.Enter);
        _app.Frame();

        Assert.Equal(TimeSpan.FromSeconds(10), _app.Settings.Watch);

        _app.Type($"!{Settings.WatchName} {Settings.WatchOff}");
        _app.Press(ConsoleKey.Enter);
        _app.Frame();

        Assert.Equal(TimeSpan.Zero, _app.Settings.Watch);
    }

    /// <summary>
    ///     While the line has the keyboard the letters on it are letters. A screen that read them as keys
    ///     would open the menu on the <c>e</c> of <c>editor</c>.
    /// </summary>
    [Fact]
    public void TheLettersTypedIntoItAreNotKeys()
    {
        _app.Type("!editor");

        Assert.Null(_app.State.Modal);
        Assert.Contains("editor", _app.FrameLines()[_app.CommandLineRow()], StringComparison.Ordinal);
    }
}
