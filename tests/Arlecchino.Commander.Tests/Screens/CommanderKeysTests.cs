using System;
using System.IO;
using System.Linq;
using Arlecchino.Commander.Files.Trash;
using Arlecchino.Commander.Views;
using Arlecchino.Input;
using Xunit;
using static Arlecchino.Commander.Localization;
using Arlecchino.Commander.Tests.Support;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
/// The function keys along the bottom, pressed. Each of them either changes the screen, opens something
/// to answer, or refuses — and refusing quietly, with nothing on screen to say why, is the failure worth
/// catching.
/// </summary>
public sealed class CommanderKeysTests : IDisposable
{
    private readonly ScreenApp _app = new(ViewKind.Commander);

    public CommanderKeysTests()
    {
        _app.Write("alpha.txt", "one");
        _app.Write(".hidden", "two");
        Directory.CreateDirectory(Path.Combine(_app.Folder, "nested"));

        _app.Sessions.Start(_app.Folder, _app.Folder);
        _app.Settled();
    }

    public void Dispose() => _app.Dispose();

    private void OnAlpha()
    {
        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.DownArrow);
        _app.Frame();
    }

    [Fact]
    public void ViewingAFileGoesToTheViewer()
    {
        OnAlpha();
        _app.Press(ConsoleKey.F3);
        _app.Frame();

        Assert.Equal(ViewKind.Viewer, _app.Navigator.CurrentRoute);
    }

    /// <summary>
    /// Every operation is asked through the same dialog, so every one of them names itself, says what
    /// it will act on, and offers the same two keys to answer with.
    /// </summary>
    [Fact]
    public void CopyingAsksBeforeItCopies()
    {
        OnAlpha();
        _app.Press(ConsoleKey.F5);

        var screen = _app.Frame();

        Assert.Contains("Copy", screen, StringComparison.Ordinal);
        Assert.Contains("WHERE", screen, StringComparison.Ordinal);
        Assert.Contains("Enter Copy", screen, StringComparison.Ordinal);
        Assert.Contains("Esc Cancel", screen, StringComparison.Ordinal);
    }

    [Fact]
    public void MakingAFolderAsksWhatToCallIt()
    {
        _app.Press(ConsoleKey.F7);

        var screen = _app.Frame();

        Assert.Contains("New folder", screen, StringComparison.Ordinal);
        Assert.Contains("NAME", screen, StringComparison.Ordinal);
        Assert.Contains("Enter Create", screen, StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing goes anywhere by the asking. What the dialog promises depends on where the file is headed: a
    /// machine with a trash says it can be fetched back out of it, one without says it cannot. The one thing
    /// this dialog must not do is offer a comfort that is not there.
    /// </summary>
    [Fact]
    public void DeletingAsksBeforeItDeletes()
    {
        OnAlpha();
        _app.Press(ConsoleKey.F8);

        var screen = _app.Frame();

        Assert.Contains("GOING AWAY", screen, StringComparison.Ordinal);
        Assert.Contains(
            Trash.Here.Works ? "out of the trash" : "no undoing it",
            screen,
            StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_app.Folder, "alpha.txt")));
    }

    /// <summary>
    /// Shift asks for the other one, which is final wherever it runs. Somebody who wants a thing gone
    /// should not have to go and empty the trash afterward.
    /// </summary>
    [Fact]
    public void DeletingForGoodSaysItCannotBeUndone()
    {
        OnAlpha();
        _app.Press(ConsoleKey.F8, shift: true);

        var screen = _app.Frame();

        Assert.Contains("Delete", screen, StringComparison.Ordinal);
        Assert.Contains("no undoing it", screen, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_app.Folder, "alpha.txt")));
    }

    /// <summary>Escape leaves everything as it was, which is what makes the dialog safe to open.</summary>
    [Fact]
    public void CallingTheDialogOffChangesNothing()
    {
        OnAlpha();
        _app.Press(ConsoleKey.F8);
        _app.Frame();
        _app.Press(ConsoleKey.Escape);

        var screen = _app.Frame();

        Assert.DoesNotContain("GOING AWAY", screen, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_app.Folder, "alpha.txt")));
    }

    /// <summary>What is typed into the one field is what the operation is given.</summary>
    [Fact]
    public void TheFolderIsMadeUnderTheNameThatWasTyped()
    {
        _app.Press(ConsoleKey.F7);
        _app.Frame();
        _app.Type("benchmarks");
        _app.Press(ConsoleKey.Enter);

        Assert.True(_app.Until(() => Directory.Exists(Path.Combine(_app.Folder, "benchmarks"))));
    }

    /// <summary>
    /// Tab finishes the path in the field, to as much as every candidate agrees on. Typing a
    /// destination out in full is the slowest thing the dialog ever asks for.
    /// </summary>
    [Fact]
    public void TabFinishesThePathInTheField()
    {
        Directory.CreateDirectory(Path.Combine(_app.Folder, "nestling"));

        _app.Sessions.Left.Marks.Clear();
        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.F5);
        _app.Frame();
        _app.Type("/nestl");
        _app.Press(ConsoleKey.Tab);

        Assert.True(_app.Until(() => _app.Frame().Contains("nestling", StringComparison.Ordinal)));
    }

    /// <summary>
    /// It completes to as much as the candidates agree on and no further. Two folders that start alike
    /// are a question the field cannot answer, so it answers the part it can.
    /// </summary>
    [Fact]
    public void TabStopsWhereTheNamesStopAgreeing()
    {
        Directory.CreateDirectory(Path.Combine(_app.Folder, "nestling"));

        _app.Sessions.Left.Marks.Clear();
        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.F5);
        _app.Frame();
        _app.Type("/nes");
        _app.Press(ConsoleKey.Tab);
        _app.Until(() => false);

        var field = Array.Find(_app.FrameLines(), line => line.Contains("/nes", StringComparison.Ordinal));

        Assert.NotNull(field);
        Assert.Contains("/nest", field, StringComparison.Ordinal);
        Assert.DoesNotContain("nestling", field, StringComparison.Ordinal);
        Assert.DoesNotContain("nested", field, StringComparison.Ordinal);
    }

    /// <summary>Tab reaches the switches and Space turns them, which is the whole of the dialog's input.</summary>
    [Fact]
    public void TabReachesTheSwitchesAndSpaceTurnsThem()
    {
        _app.Press(ConsoleKey.F7);
        _app.Frame();

        Assert.Contains("[×] jump the cursor onto it", _app.Frame(), StringComparison.Ordinal);

        _app.Press(ConsoleKey.Tab);
        _app.Press(ConsoleKey.Spacebar);

        Assert.Contains("[ ] jump the cursor onto it", _app.Frame(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The palette is the way to everything the bar along the bottom has no room for. It holds the
    /// menu entry by entry as well as the keys, so nothing has to be found by remembering where it was
    /// filed.
    /// </summary>
    [Fact]
    public void ThePaletteHoldsEverythingTheBarDoesNot()
    {
        _app.Press(ConsoleKey.K, control: true);

        var screen = _app.Frame();

        Assert.Contains("Do anything", screen, StringComparison.Ordinal);
        Assert.Contains("Find file", screen, StringComparison.Ordinal);
        Assert.Contains("Enter run", screen, StringComparison.Ordinal);
        Assert.Contains("Tab complete", screen, StringComparison.Ordinal);
    }

    /// <summary>Typing narrows it, and the count says by how much.</summary>
    [Fact]
    public void TypingNarrowsThePalette()
    {
        _app.Press(ConsoleKey.K, control: true);
        _app.Frame();
        _app.Type("hotlist");

        var screen = _app.Frame();

        Assert.Contains("Hotlist", screen, StringComparison.Ordinal);
        Assert.Contains(" of ", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("Find file", screen, StringComparison.Ordinal);
    }

    /// <summary>Picking a row runs it, which is the whole point of a list of actions.</summary>
    [Fact]
    public void PickingFromThePaletteRunsIt()
    {
        _app.Press(ConsoleKey.K, control: true);
        _app.Frame();
        _app.Type("hidden files here");
        _app.Press(ConsoleKey.Enter);

        Assert.True(_app.Until(() => _app.Frame().Contains(".hidden", StringComparison.Ordinal)));
    }

    [Fact]
    public void TheMenuOpensOnTheKeyItIsLabelledWith()
    {
        _app.Press(ConsoleKey.F9);
        _app.Frame();

        Assert.NotNull(_app.State.Modal);
    }

    /// <summary><c>F2</c> lists the tabs and the two things worth doing to them.</summary>
    [Fact]
    public void TheTabsOpenOnTheKeyTheBarNames()
    {
        _app.Sessions.Add();
        _app.Press(ConsoleKey.F2);

        var screen = _app.Frame();

        Assert.Contains("Tabs", screen, StringComparison.Ordinal);
        Assert.Contains("New tab", screen, StringComparison.Ordinal);
        Assert.Contains("Close this tab", screen, StringComparison.Ordinal);
        Assert.Contains("1 · local ⇄ local", screen, StringComparison.Ordinal);
        Assert.Contains("on screen", screen, StringComparison.Ordinal);
    }

    /// <summary>
    /// A tab is in the palette by the name it wears in the band, so the one on a server is reached by
    /// typing the name of the server rather than by counting tabs along the top.
    /// </summary>
    [Fact]
    public void ThePaletteGoesToATabByName()
    {
        _app.Sessions.Add();
        _app.Settled();

        Assert.Equal(1, _app.Sessions.Open.Value);

        _app.Press(ConsoleKey.K, control: true);
        _app.Frame();
        _app.Type("1 · local");
        _app.Press(ConsoleKey.Enter);
        _app.Settled();

        Assert.Equal(0, _app.Sessions.Open.Value);
    }

    /// <summary>
    /// What a key does is written down once. The palette lists every command of the screen, so the tab
    /// rows must not carry the keys as well — a row offered twice is a list that has stopped saying
    /// anything about where a thing lives.
    /// </summary>
    [Fact]
    public void ThePaletteOffersNewTabOnce()
    {
        _app.Press(ConsoleKey.K, control: true);
        _app.Frame();
        _app.Type("new tab");

        var lines = _app.FrameLines();
        var offered = lines.Count(line => line.Contains("New tab", StringComparison.Ordinal));

        Assert.Equal(1, offered);
    }

    [Fact]
    public void FilteringAsksForThePattern()
    {
        _app.Press(ConsoleKey.F4);
        _app.Frame();

        Assert.NotNull(_app.State.Modal);
    }

    [Fact]
    public void HiddenFilesAreShownAndHiddenAgain()
    {
        Assert.DoesNotContain(".hidden", _app.Frame(), StringComparison.Ordinal);

        _app.Press(ConsoleKey.X);
        _app.Press(ConsoleKey.I);
        Assert.Contains(".hidden", _app.Frame(), StringComparison.Ordinal);

        _app.Press(ConsoleKey.X);
        _app.Press(ConsoleKey.I);
        Assert.DoesNotContain(".hidden", _app.Frame(), StringComparison.Ordinal);
    }

    [Fact]
    public void SwappingPutsEachPanelWhereTheOtherWas()
    {
        var other = Directory.CreateDirectory(Path.Combine(_app.Folder, "other")).FullName;

        _app.Sessions.Right.GoTo(other);
        _app.Frame();

        var left = _app.Sessions.Left.Folder;
        var right = _app.Sessions.Right.Folder;

        _app.Press(ConsoleKey.U, control: true);
        _app.Frame();

        Assert.Equal(right, _app.Sessions.Left.Folder);
        Assert.Equal(left, _app.Sessions.Right.Folder);
    }

    [Fact]
    public void ReloadingKeepsThePanelWhereItWas()
    {
        var where = _app.Sessions.Left.Folder;

        _app.Write("appeared.txt", "three");
        _app.Press(ConsoleKey.X);
        _app.Press(ConsoleKey.R);

        var screen = _app.Frame();

        Assert.Equal(where, _app.Sessions.Left.Folder);
        Assert.Contains("appeared.txt", screen, StringComparison.Ordinal);
    }

    /// <summary>
    /// The way out of a folder is the <c>..</c> row, and only that. Backspace used to do it as well and
    /// no longer does: the command line is typed on without ever taking the focus, so a Backspace meant
    /// for a typo would leave the folder instead.
    /// </summary>
    [Fact]
    public void GoingUpLeavesTheFolder()
    {
        var nested = Path.Combine(_app.Folder, "nested");

        _app.Sessions.Left.GoTo(nested);
        _app.Sessions.Moved();
        _app.Settled();

        _app.Settled();
        _app.Press(ConsoleKey.Home);
        _app.Press(ConsoleKey.Enter);

        Assert.True(_app.Until(() => _app.Sessions.Left.Folder == _app.Folder));
    }

    [Fact]
    public void BackspaceIsLeftToTheCommandLine()
    {
        var nested = Path.Combine(_app.Folder, "nested");

        _app.Sessions.Left.GoTo(nested);
        _app.Sessions.Moved();
        _app.Settled();

        _app.Type(":ls x");
        _app.Press(ConsoleKey.Backspace);

        Assert.Equal(nested, _app.Sessions.Left.Folder);
        Assert.Contains("ls ", _app.Frame(), StringComparison.Ordinal);
        Assert.DoesNotContain("ls x", _app.Frame(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Every key the bar advertises has something behind it. A label with nothing behind it is a key
    /// that does nothing when it is pressed, and the bar is the only instruction most people read.
    /// </summary>
    [Fact]
    public void EveryKeyTheBarAdvertisesIsOneTheViewKnows()
    {
        var bar = _app.BarLine();
        var known = _app.Navigator.CurrentCommands.Select(static command => command.Label()).ToList();

        foreach (var key in new[] { "F3", "F5", "F8" })
        {
            Assert.Contains(key, bar, StringComparison.Ordinal);
        }

        Assert.Contains(Loc(LocString.View), known);
        Assert.Contains(Loc(LocString.Copy), known);
        Assert.Contains(Loc(LocString.Delete), known);
    }

    /// <summary>
    /// Escape belongs to whatever the screen shows: it ends the search that runs while you type, and it
    /// leaves a filter, a dialog or a screen. Nothing on this screen claims it.
    /// </summary>
    [Fact]
    public void EscapeEndsTheSearch()
    {
        _app.Press(ConsoleKey.S, control: true);
        _app.Type("al");

        Assert.Contains("jump to", _app.Frame(), StringComparison.Ordinal);

        _app.Press(ConsoleKey.Escape);

        Assert.DoesNotContain("jump to", _app.Frame(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Stopping the work is Escape behind the leader. It was plain Escape, which meant one key stood for
    /// "get out of this" most of the time and "stop the copy" whenever something was running — and you cannot
    /// press a key you have to think about first. It was held with Alt afterward, which a Mac terminal
    /// never sends, so it now sits where the rest of the operations are.
    /// </summary>
    [Fact]
    public void StoppingTheWorkIsEscapeBehindTheLeader()
    {
        var stop = _app.Navigator.CurrentCommands
            .Single(command => command.Label() == Loc(LocString.KeyStop));

        Assert.True(stop.Binding.IsChord);
        Assert.True(stop.Binding.Opens(new(ConsoleKey.X)));
        Assert.True(stop.Binding.Closes(new(ConsoleKey.Escape)));
        Assert.False(stop.Binding.Matches(new(ConsoleKey.Escape)));
    }

    /// <summary>The pair opens what the letter behind it stands for, and neither half does it alone.</summary>
    [Fact]
    public void APairOpensWhatTheLetterBehindItStandsFor()
    {
        OnAlpha();

        _app.Press(ConsoleKey.C);

        Assert.DoesNotContain(Loc(LocString.Permissions), _app.Frame(), StringComparison.Ordinal);

        _app.Press(ConsoleKey.X);
        _app.Press(ConsoleKey.C);

        Assert.Contains(Loc(LocString.Permissions), _app.Frame(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A leader on its own puts what finishes it in the box in the corner. That is the whole reason the
    /// keys are grouped behind one: the second key is read rather than remembered.
    /// </summary>
    [Fact]
    public void ALeaderListsWhatFinishesIt()
    {
        _app.Press(ConsoleKey.X);

        var frame = _app.Frame();

        Assert.Contains(Loc(LocString.Permissions), frame, StringComparison.Ordinal);
        Assert.Contains(Loc(LocString.MenuHardLink), frame, StringComparison.Ordinal);
    }

    /// <summary>
    /// A laptop has no <c>PgUp</c>, so going up a folder is spelled out behind the leader — and the
    /// keyboard that does have the key keeps it, since the pair carries it as an alternative.
    /// </summary>
    [Fact]
    public void GoingUpAnswersToTheLetterAndToTheKeyTheFullKeyboardHas()
    {
        var up = _app.Navigator.CurrentCommands
            .Single(command => command.Label() == Loc(LocString.KeyFolderAbove));

        Assert.True(up.Binding.IsChord);
        Assert.True(up.Binding.Opens(new(ConsoleKey.G)));
        Assert.True(up.Binding.Matches(new(ConsoleKey.PageUp, KeyModifiers.Control)));
        Assert.StartsWith("G ", up.Binding.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Every tab key is behind the one leader, so there is a single place to look for the five of them.
    /// They were spread between a function key, two Control letters and the leader that goes places.
    /// </summary>
    [Fact]
    public void TheTabKeysAreAllBehindTheirOwnLeader()
    {
        var labels = new[]
        {
            LocString.TabsNew,
            LocString.TabsClose,
            LocString.TabsNext,
            LocString.TabsPrevious,
            LocString.TabsTitle,
        };

        var tabs = _app.Navigator.CurrentCommands
            .Where(command => labels.Any(label => command.Label() == Loc(label)))
            .ToList();

        Assert.Equal(labels.Length, tabs.Count);
        Assert.All(tabs, command => Assert.True(command.Binding.Opens(new(ConsoleKey.T))));
    }

    /// <summary>
    /// The function keys as a terminal speaking the keyboard protocol really sends them. These bytes were
    /// read off kitty rather than written from the specification: with the protocol on, the first four
    /// arrive as <c>CSI P Q S</c> and — because <c>CSI R</c> is how a terminal answers where its cursor
    /// is — F3 arrives as <c>CSI 13~</c> instead.
    /// </summary>
    [Theory]
    [InlineData("\e[P", "Keys")]
    [InlineData("\e[Q", "Tabs")]
    [InlineData("\e[S", "Filter")]
    public void TheFunctionKeysArriveAsTheProtocolSendsThem(string sequence, string expected)
    {
        _app.ReadFromTerminal(sequence);

        Assert.Contains(expected, _app.Frame(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The letters under the right hand move the cursor exactly as the arrows do. They are handed on as
    /// arrows rather than moving the cursor themselves, so there is only one place where paging and
    /// wrapping are decided.
    /// </summary>
    [Fact]
    public void TheLettersUnderTheRightHandMoveLikeTheArrows()
    {
        _app.Press(ConsoleKey.J);
        _app.Press(ConsoleKey.J);

        var lettered = _app.Frame();

        _app.Press(ConsoleKey.K);
        _app.Press(ConsoleKey.K);
        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.DownArrow);

        Assert.Equal(lettered, _app.Frame());
    }

    /// <summary>
    /// Out of a folder and into one, which is what a file manager built on these keys means by them —
    /// left and right already switch panels, so the letters are worth more spent this way.
    /// </summary>
    [Fact]
    public void TheOuterLettersLeaveAndEnterAFolder()
    {
        var where = _app.Sessions.Left.Folder;

        _app.Press(ConsoleKey.H);
        _app.Settled();

        Assert.NotEqual(where, _app.Sessions.Left.Folder);

        _app.Press(ConsoleKey.L);
        _app.Settled();

        Assert.Equal(where, _app.Sessions.Left.Folder);
    }

    /// <summary>Nothing on the screen is reachable through a page key and nothing else.</summary>
    [Fact]
    public void NoKeyNeedsAPageKeyToBeReached()
    {
        var paged = _app.Navigator.CurrentCommands
            .Where(command => command.Binding.Matches(new(ConsoleKey.PageUp, KeyModifiers.Control)) ||
                              command.Binding.Matches(new(ConsoleKey.PageDown, KeyModifiers.Control)) ||
                              command.Binding.Matches(new(ConsoleKey.PageUp, KeyModifiers.Alt)) ||
                              command.Binding.Matches(new(ConsoleKey.PageDown, KeyModifiers.Alt)));

        Assert.All(paged, command => Assert.True(command.Binding.IsChord));
    }

    /// <summary>
    /// The key after a leader belongs to the pair and to nothing else. Were it let through, a letter
    /// pressed after a mistyped leader would run whatever that letter means on its own.
    /// </summary>
    [Fact]
    public void TheKeyAfterALeaderReachesNothingElse()
    {
        _app.Press(ConsoleKey.X);
        _app.Press(ConsoleKey.F9);

        Assert.DoesNotContain(Loc(LocString.MenuMakeFolder), _app.Frame(), StringComparison.Ordinal);
    }
}
