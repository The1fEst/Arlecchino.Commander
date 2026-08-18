using System;
using System.IO;
using System.Linq;
using Arlecchino.Commander.Files.Trash;
using Arlecchino.Commander.Model;
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

    /// <summary>
    /// Reading a file is Enter on it, the key that opens whatever the cursor is on. The bar has no key
    /// spent on it, since the key everyone reaches for already does it.
    /// </summary>
    [Fact]
    public void ViewingAFileGoesToTheViewer()
    {
        OnAlpha();
        _app.Press(ConsoleKey.Enter);
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
    /// Nothing goes anywhere by the asking. A machine with a trash says the file can be fetched back out
    /// of it, and one without says it cannot.
    /// </summary>
    [Fact]
    public void DeletingAsksBeforeItDeletes()
    {
        OnAlpha();
        _app.Press(ConsoleKey.F8);

        var screen = _app.Frame();

        Assert.Contains("GOING AWAY", screen, StringComparison.Ordinal);
        Assert.Contains(
            Trash.Current.Works ? "out of the trash" : "no undoing it",
            screen,
            StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_app.Folder, "alpha.txt")));
    }

    /// <summary>
    /// Shift asks for the other one, which is final wherever it runs. Wanting a thing gone should not
    /// mean emptying the trash afterward.
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
        _app.Type("hard link");

        var screen = _app.Frame();

        Assert.Contains("Hard link", screen, StringComparison.Ordinal);
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
    /// rows must not carry the keys as well.
    /// </summary>
    [Fact]
    public void ThePaletteOffersNewTabOnce()
    {
        _app.Press(ConsoleKey.K, control: true);
        _app.Frame();
        _app.Type("new tab");

        var lines = _app.FrameLines();
        var count = lines.Count(line => line.Contains("New tab", StringComparison.Ordinal));

        Assert.Equal(1, count);
    }

    /// <summary>
    ///     Editing is the editor the settings name, so a screen that has been told of none says so rather
    ///     than opening something of its own. There is no editor of our own to open.
    /// </summary>
    [Fact]
    public void EditingWithNoEditorSetSaysSo()
    {
        OnAlpha();
        _app.Press(ConsoleKey.F3);

        Assert.Contains("No editor set", _app.Frame(), StringComparison.Ordinal);
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
        var folder = _app.Sessions.Left.Folder;

        _app.Write("appeared.txt", "three");
        _app.Press(ConsoleKey.X);
        _app.Press(ConsoleKey.R);

        var screen = _app.Frame();

        Assert.Equal(folder, _app.Sessions.Left.Folder);
        Assert.Contains("appeared.txt", screen, StringComparison.Ordinal);
    }

    /// <summary>
    /// The way out of a folder is the <c>..</c> row, and only that. The command line is typed on without
    /// taking the focus, so a Backspace meant for a typo must not leave the folder.
    /// </summary>
    [Fact]
    public void GoingUpLeavesTheFolder()
    {
        var folder = Path.Combine(_app.Folder, "nested");

        _app.Sessions.Left.GoTo(folder);
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
        var folder = Path.Combine(_app.Folder, "nested");

        _app.Sessions.Left.GoTo(folder);
        _app.Sessions.Moved();
        _app.Settled();

        _app.Type(":ls x");
        _app.Press(ConsoleKey.Backspace);

        Assert.Equal(folder, _app.Sessions.Left.Folder);
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
        var hosts = _app.Navigator.CurrentCommands.Select(static command => command.Label()).ToList();

        foreach (var key in new[] { "F3", "F5", "F8" })
        {
            Assert.Contains(key, bar, StringComparison.Ordinal);
        }

        Assert.Contains(Loc(LocString.Edit), hosts);
        Assert.Contains(Loc(LocString.Copy), hosts);
        Assert.Contains(Loc(LocString.Delete), hosts);
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
    /// Stopping the work is Escape behind the leader, where the rest of the operations are. Plain Escape
    /// already stands for getting out of things, and Alt is a modifier a Mac terminal never sends.
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
    /// A leader on its own puts what finishes it on the screen, drawn by this screen rather than by the
    /// framework. The framework fences it in a titled box, and nothing else here is fenced in.
    /// </summary>
    [Fact]
    public void ALeaderListsWhatFinishesIt()
    {
        _app.Press(ConsoleKey.X);

        var frame = _app.Frame();

        Assert.Contains(Loc(LocString.MenuKeys).ToUpperInvariant(), frame, StringComparison.Ordinal);
        Assert.Contains(Loc(LocString.Permissions), frame, StringComparison.Ordinal);
        Assert.Contains(Loc(LocString.LinkHard), frame, StringComparison.Ordinal);
        Assert.DoesNotContain("╭─", frame, StringComparison.Ordinal);
    }

    /// <summary>The list goes away with the chord, so it is only ever answering a question being asked.</summary>
    [Fact]
    public void WhatFinishesTheLeaderGoesWithIt()
    {
        _app.Press(ConsoleKey.X);
        _app.Frame();
        _app.Press(ConsoleKey.Escape);

        Assert.DoesNotContain(Loc(LocString.MenuKeys).ToUpperInvariant(), _app.Frame(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Going up a folder answers to all three: the letter an editor puts it on, the arrow that points
    /// that way, and the pair a full keyboard has. It is named after the letter.
    /// </summary>
    [Fact]
    public void GoingUpAnswersToTheLetterTheArrowAndTheKeyTheFullKeyboardHas()
    {
        var up = _app.Navigator.CurrentCommands
            .Single(command => command.Label() == Loc(LocString.KeyFolderAbove));

        Assert.True(up.Binding.Matches(new(ConsoleKey.H)));
        Assert.True(up.Binding.Matches(new(ConsoleKey.LeftArrow)));
        Assert.True(up.Binding.Matches(new(ConsoleKey.PageUp, KeyModifiers.Control)));
        Assert.Equal("H", up.Binding.ToString());
    }

    /// <summary>
    /// Every tab key is behind the one leader, so there is a single place to look for the four of them.
    /// Listing the tabs is the exception, being what the bar spends <c>F2</c> on.
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
        };

        var tabs = _app.Navigator.CurrentCommands
            .Where(command => labels.Any(label => command.Label() == Loc(label)))
            .ToList();

        Assert.Equal(labels.Length, tabs.Count);
        Assert.All(tabs, command => Assert.True(command.Binding.Opens(new(ConsoleKey.T))));
    }

    /// <summary>
    /// The function keys as a terminal speaking the keyboard protocol really sends them, read off kitty
    /// rather than written from the specification.
    /// </summary>
    /// <param name="sequence">The bytes the terminal sends.</param>
    /// <param name="expected">What the screen says once they arrive.</param>
    [Theory]
    [InlineData("\e[P", "Keys")]
    [InlineData("\e[Q", "Tabs")]
    [InlineData("\e[R", "No editor set")]
    [InlineData("\e[S", "Find files")]
    public void TheFunctionKeysArriveAsTheProtocolSendsThem(string sequence, string expected)
    {
        OnAlpha();
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

        var screen = _app.Frame();

        _app.Press(ConsoleKey.K);
        _app.Press(ConsoleKey.K);
        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.DownArrow);

        Assert.Equal(screen, _app.Frame());
    }

    /// <summary>
    /// Out of a folder and into one, which is what a file manager built on these keys means by them —
    /// left and right already switch panels, so the letters are worth more spent this way.
    /// </summary>
    [Fact]
    public void TheOuterLettersLeaveAndEnterAFolder()
    {
        var folder = _app.Sessions.Left.Folder;

        _app.Press(ConsoleKey.H);
        _app.Settled();

        Assert.NotEqual(folder, _app.Sessions.Left.Folder);

        _app.Press(ConsoleKey.L);
        _app.Settled();

        Assert.Equal(folder, _app.Sessions.Left.Folder);
    }

    /// <summary>
    /// A key for each column and one more to turn the order around. Asking for the column it is already
    /// on turns it around too, as a click on the column head does.
    /// </summary>
    [Fact]
    public void SortingIsAColumnEachAndAWayBack()
    {
        _app.Press(ConsoleKey.S);
        _app.Press(ConsoleKey.J);
        _app.Settled();

        Assert.Equal(Sorting.Size, _app.Sessions.Left.Sorting);
        Assert.False(_app.Sessions.Left.Descending);

        _app.Press(ConsoleKey.S);
        _app.Press(ConsoleKey.L);
        _app.Settled();

        Assert.Equal(Sorting.Size, _app.Sessions.Left.Sorting);
        Assert.True(_app.Sessions.Left.Descending);
    }

    /// <summary>The tag column sorts too, off the key the other columns are on.</summary>
    [Fact]
    public void SortingByKindIsOneOfTheColumns()
    {
        _app.Press(ConsoleKey.S);
        _app.Press(ConsoleKey.T);
        _app.Settled();

        Assert.Equal(Sorting.Kind, _app.Sessions.Left.Sorting);
    }

    /// <summary>
    /// A search running on the panel has the letters, leaders and all. Every leader is a letter a file
    /// name can begin with.
    /// </summary>
    [Fact]
    public void ASearchKeepsTheLettersTheLeadersAreOn()
    {
        _app.Press(ConsoleKey.Oem2);
        _app.Type("gsxt");

        var screen = _app.Frame();

        Assert.Contains("gsxt", screen, StringComparison.Ordinal);
        Assert.DoesNotContain(Loc(LocString.MenuSortByName), screen, StringComparison.Ordinal);
        Assert.DoesNotContain(Loc(LocString.TabsNew), screen, StringComparison.Ordinal);
    }

    /// <summary>
    /// Enter ends the search and then opens what it found, in the one press. Escape is the one key the
    /// search keeps, since it says stop and nothing else.
    /// </summary>
    [Fact]
    public void EnterEndsTheSearchAndOpensWhatItFound()
    {
        var folder = _app.Sessions.Left.Folder;

        _app.Press(ConsoleKey.Oem2);
        _app.Type("nes");
        _app.Press(ConsoleKey.Enter);
        _app.Settled();

        Assert.Equal(Path.Combine(folder, "nested"), _app.Sessions.Left.Folder);
        Assert.DoesNotContain("jump to", _app.Frame(), StringComparison.Ordinal);
    }

    /// <summary>Nothing on the screen is reachable through a page key and nothing else.</summary>
    [Fact]
    public void NoKeyNeedsAPageKeyToBeReached()
    {
        var pageKeyCommands = _app.Navigator.CurrentCommands
            .Where(command => command.Binding.Matches(new(ConsoleKey.PageUp, KeyModifiers.Control)) ||
                              command.Binding.Matches(new(ConsoleKey.PageDown, KeyModifiers.Control)) ||
                              command.Binding.Matches(new(ConsoleKey.PageUp, KeyModifiers.Alt)) ||
                              command.Binding.Matches(new(ConsoleKey.PageDown, KeyModifiers.Alt)));

        Assert.All(
            pageKeyCommands,
            command => Assert.True(command.Binding.IsChord || command.Binding.First.Key is >= ConsoleKey.A and <= ConsoleKey.Z));
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
        _app.Frame();

        Assert.Null(_app.State.Modal);
    }
}
