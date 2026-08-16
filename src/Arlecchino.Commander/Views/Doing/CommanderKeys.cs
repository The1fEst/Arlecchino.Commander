using System;
using System.Collections.Generic;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Commands;
using Arlecchino.Input;
using Arlecchino.Navigation;
using Microsoft.Extensions.Hosting;

namespace Arlecchino.Commander.Views.Doing;

/// <summary>
/// Which key does which of the things the screen can do. It is a table and nothing else: every key is a
/// call to <see cref="Doings"/>, which is what lets the palette list the keys and the menu together.
/// </summary>
public static class CommanderKeys
{
    /// <summary>Builds the table.</summary>
    /// <param name="doings">Everything the screen can do.</param>
    /// <param name="panels">The two panels on screen.</param>
    /// <param name="sessions">The tabs, which four of these keys open, close and step between.</param>
    /// <param name="operations">The file work, which the stop key calls off.</param>
    /// <param name="runner">The commands, which the stop key stops.</param>
    /// <param name="commandBar">The command line, which four of these keys write to or open.</param>
    /// <param name="setting">The settings line, which one of these keys opens.</param>
    /// <param name="lifetime">How the application is quit.</param>
    /// <returns>Every key the screen answers to.</returns>
    public static IReadOnlyList<ViewCommand> For(
        Doings doings,
        Pair panels,
        Sessions sessions,
        Operations operations,
        Runner runner,
        CommandBar commandBar,
        SettingBar setting,
        IHostApplicationLifetime lifetime)
    {
        return
        [
            Bind.To(new(ConsoleKey.F1, KeyModifiers.Control),
                LocString.KeyDriveLeft,
                () => doings.ChooseDrive(panels.Left)),
            Bind.To(new(ConsoleKey.F2, KeyModifiers.Control),
                LocString.KeyDriveRight,
                () => doings.ChooseDrive(panels.Right)),
            Bind.Going(new(ConsoleKey.F1),
                LocString.BarHelp,
                () => ViewKind.Keys),
            Bind.To(new(ConsoleKey.F2),
                LocString.TabsTitle,
                () => TabList.Open(doings)),
            Bind.To(new(ConsoleKey.F3),
                LocString.Edit,
                doings.Editing.Edit),
            Bind.Going(new(ConsoleKey.F4),
                LocString.FindVerb,
                doings.Find),
            Marked(new(ConsoleKey.F5),
                LocString.Copy,
                panels,
                doings.Files.Copy),
            Marked(new(ConsoleKey.F6),
                LocString.Move,
                panels,
                doings.Files.Move),
            Bind.To(new(ConsoleKey.F6, KeyModifiers.Shift),
                LocString.Rename,
                doings.Files.Rename),
            Bind.To(new(ConsoleKey.F7),
                LocString.MenuMakeFolder,
                doings.Files.MakeFolder),
            Marked(new(ConsoleKey.F8),
                LocString.Delete,
                panels,
                doings.Files.Delete),
            Bind.To(new(ConsoleKey.F8, KeyModifiers.Shift),
                LocString.MenuDeleteForGood,
                doings.Files.DeleteForGood),
            Bind.To(new(ConsoleKey.F9),
                LocString.Menu,
                () => Menu.Open(doings)),
            Bind.To(new(ConsoleKey.F10),
                LocString.BarQuit,
                lifetime.StopApplication),
            Bind.To(new(ConsoleKey.U, KeyModifiers.Control),
                LocString.MenuSwapPanels,
                doings.Swap),
            Bind.To(Searching(),
                LocString.KeySearch,
                () => panels.Active.Search()),
            Bind.To(new(':'),
                LocString.KeyCommandLine,
                commandBar.Open),
            Bind.To(new('!'),
                LocString.KeySettingsLine,
                setting.Open),
            Bind.To(new(ConsoleKey.K, KeyModifiers.Control),
                LocString.PaletteTitle,
                () => Menu.Palette(doings)),
            Bind.Going(new(ConsoleKey.O, KeyModifiers.Control),
                LocString.MenuWhatCommandsSaid,
                static () => ViewKind.Output),

            Bind.To(Leaving(),
                LocString.KeyFolderAbove,
                () => panels.Active.Ascend()),
            Bind.To(Entering(),
                LocString.KeyOpenFolder,
                () => panels.Active.Descend()),
            Bind.To(Go(ConsoleKey.H),
                LocString.KeyBack,
                doings.Back),
            Bind.To(Go(ConsoleKey.L),
                LocString.KeyForward,
                doings.Forward),
            Bind.To(Go(ConsoleKey.U),
                LocString.KeyTop,
                () => panels.Active.Top()),
            Bind.To(Go(ConsoleKey.O),
                LocString.KeyMiddle,
                () => panels.Active.Middle()),
            Bind.To(Go(ConsoleKey.M),
                LocString.KeyBottom,
                () => panels.Active.Bottom()),
            Bind.To(Go(ConsoleKey.B),
                LocString.MenuBothPanelsHere,
                () => panels.Passive.GoTo(panels.Active.Folder)),
            Bind.To(Go(ConsoleKey.Y),
                LocString.KeyOtherPanelInto,
                doings.Beside),
            Bind.To(Go(ConsoleKey.N),
                LocString.MenuOpenSavedHost,
                () => doings.Dialling.Saved(panels.Active)),

            Bind.To(Tab(ConsoleKey.K),
                LocString.TabsNew,
                sessions.Add),
            Bind.When(Tab(ConsoleKey.J),
                LocString.TabsClose,
                Several(sessions),
                () => Closed(sessions)),
            Bind.When(Tab(ConsoleKey.L),
                LocString.TabsNext,
                Several(sessions),
                () => Stepped(sessions, forward: true)),
            Bind.When(Tab(ConsoleKey.H),
                LocString.TabsPrevious,
                Several(sessions),
                () => Stepped(sessions, forward: false)),

            Bind.To(Sorted(ConsoleKey.H),
                LocString.MenuSortByName,
                () => panels.Active.SortBy(Sorting.Name)),
            Bind.To(Sorted(ConsoleKey.J),
                LocString.MenuSortBySize,
                () => panels.Active.SortBy(Sorting.Size)),
            Bind.To(Sorted(ConsoleKey.K),
                LocString.MenuSortByDate,
                () => panels.Active.SortBy(Sorting.Modified)),
            Bind.To(Sorted(ConsoleKey.T),
                LocString.MenuSortByKind,
                () => panels.Active.SortBy(Sorting.Kind)),
            Bind.To(Sorted(ConsoleKey.L),
                LocString.MenuSortReversed,
                () => panels.Active.Reverse()),

            Bind.To(Execute(ConsoleKey.C),
                LocString.Permissions,
                doings.Rights.Mode),
            Bind.To(Execute(ConsoleKey.O),
                LocString.MenuOwner,
                doings.Rights.Owner),
            Bind.To(Execute(ConsoleKey.S),
                LocString.LinkSymbolic,
                () => doings.Linking.Make(hard: false)),
            Bind.To(Execute(ConsoleKey.L),
                LocString.LinkHard,
                () => doings.Linking.Make(hard: true)),
            Bind.To(Execute(ConsoleKey.D),
                LocString.MenuCompareDirectories,
                doings.Compare),
            Bind.To(Execute(ConsoleKey.N),
                LocString.KeyNameOntoLine,
                () => Named(panels, commandBar)),
            Bind.To(Execute(ConsoleKey.P),
                LocString.KeyFolderOntoLine,
                () => commandBar.Insert(panels.Active.Folder)),
            Bind.To(Execute(ConsoleKey.T),
                LocString.KeyNamesOntoLine,
                () => Names(panels, commandBar)),
            Bind.To(Execute(ConsoleKey.Y),
                LocString.MenuCopyPaths,
                doings.CopyPaths),
            Bind.To(Execute(ConsoleKey.R),
                LocString.MenuReload,
                doings.Reload),
            Bind.To(Execute(ConsoleKey.I),
                LocString.MenuShowHidden,
                () => doings.ToggleHidden(panels.Active)),
            Bind.When(Execute(ConsoleKey.Escape),
                LocString.KeyStop,
                () => operations.IsBusy || runner.IsRunning,
                () => Stop(operations, runner)),
        ];
    }

    /// <summary>
    /// A key that acts on what is marked, and says so: with something in hand the question is no longer
    /// what this row is but what happens to the rows that were marked, so the count goes in the name.
    /// </summary>
    /// <param name="binding">The key.</param>
    /// <param name="name">Which string names it.</param>
    /// <param name="panels">The two panels on screen, the marks being read off whichever is active.</param>
    /// <param name="run">What it does.</param>
    /// <returns>The command.</returns>
    private static ViewCommand Marked(KeyBinding binding, LocString name, Pair panels, Action run) =>
        ViewCommand.For(binding, () => Loc(name) + Held(panels), run);

    /// <summary>What the marks add to a name: nothing at all when none are marked.</summary>
    /// <param name="panels">The two panels on screen.</param>
    /// <returns>The count, worded, or an empty string.</returns>
    private static string Held(Pair panels)
    {
        var panel = panels.Active;

        if (panel.State.Marks.Count == 0)
        {
            return "";
        }

        var held = panel.Targets().Count;

        return held == 1
            ? Loc(LocString.BarOneItem)
            : Loc(LocString.BarManyItems, held);
    }

    /// <summary>A key behind the <c>t</c> leader, which is the one the tabs live behind.</summary>
    /// <param name="key">The key that finishes it.</param>
    /// <returns>The chord.</returns>
    private static KeyBinding Tab(ConsoleKey key) => new KeyBinding(ConsoleKey.T).ThenKey(key);

    /// <summary>
    /// A key behind the <c>g</c> leader, which is the one that takes you somewhere: to a row, to a folder
    /// the panel has been in, to a host.
    /// </summary>
    /// <param name="key">The key that finishes it.</param>
    /// <returns>The chord.</returns>
    private static KeyBinding Go(ConsoleKey key) => new KeyBinding(ConsoleKey.G).ThenKey(key);

    /// <summary>
    /// A key behind the <c>s</c> leader, which is the one that puts the panel in order. Three keys for the
    /// three columns and a fourth to turn the order around, all under the hand that is not reaching.
    /// </summary>
    /// <param name="key">The key that finishes it.</param>
    /// <returns>The chord.</returns>
    private static KeyBinding Sorted(ConsoleKey key) => new KeyBinding(ConsoleKey.S).ThenKey(key);

    /// <summary>
    /// Out of the folder: the letter an editor puts it on, the arrow that points that way, and the pair
    /// Midnight Commander taught the hands.
    /// </summary>
    /// <returns>The binding.</returns>
    private static KeyBinding Leaving() =>
        new KeyBinding(ConsoleKey.H)
            .AddAlternative(ConsoleKey.LeftArrow)
            .AddAlternative(ConsoleKey.PageUp, KeyModifiers.Control);

    /// <summary>Into the folder under the cursor, on the same three.</summary>
    /// <returns>The binding.</returns>
    private static KeyBinding Entering() =>
        new KeyBinding(ConsoleKey.L)
            .AddAlternative(ConsoleKey.RightArrow)
            .AddAlternative(ConsoleKey.PageDown, KeyModifiers.Control);

    /// <summary>
    /// A key behind the <c>x</c> leader, which is the one that does something to what the panel is
    /// showing. These are the letters Midnight Commander puts behind its own <c>Ctrl+X</c>.
    /// </summary>
    /// <param name="key">The key that finishes it.</param>
    /// <returns>The chord.</returns>
    private static KeyBinding Execute(ConsoleKey key) => new KeyBinding(ConsoleKey.X).ThenKey(key);

    /// <summary>
    /// Search as you type, asked for by the slash so that the letters after it are the search and not
    /// the keys. Control and Alt answer as well, as they do on Midnight Commander.
    /// </summary>
    /// <returns>The binding.</returns>
    private static KeyBinding Searching() =>
        new KeyBinding('/')
            .AddAlternative(ConsoleKey.S, KeyModifiers.Control)
            .AddAlternative(ConsoleKey.S, KeyModifiers.Alt);

    /// <summary>Whether there is more than one tab, which is what the three tab keys wait for.</summary>
    /// <param name="sessions">The tabs.</param>
    /// <returns>Whether those keys are available.</returns>
    private static Func<bool> Several(Sessions sessions) => () => sessions.All.Count > 1;

    /// <summary>Closes the tab on screen.</summary>
    /// <param name="sessions">The tabs.</param>
    /// <returns>Nowhere: closing a tab uncovers another one rather than leaving the screen.</returns>
    private static ViewRoute Closed(Sessions sessions)
    {
        sessions.Close(sessions.Current);

        return ViewRoute.None;
    }

    /// <summary>Goes to the tab beside this one.</summary>
    /// <param name="sessions">The tabs.</param>
    /// <param name="forward">Which way.</param>
    /// <returns>Nowhere.</returns>
    private static ViewRoute Stepped(Sessions sessions, bool forward)
    {
        sessions.Step(forward);

        return ViewRoute.None;
    }

    /// <summary>Puts the name under the cursor on the command line.</summary>
    /// <param name="panels">The two panels on screen.</param>
    /// <param name="commandBar">The command line.</param>
    private static void Named(Pair panels, CommandBar commandBar)
    {
        if (panels.Active.Current is { IsParent: false } current)
        {
            commandBar.Insert(current.Name);
        }
    }

    /// <summary>Puts every marked name on the command line, or the one under the cursor when none are marked.</summary>
    /// <param name="panels">The two panels on screen.</param>
    /// <param name="commandBar">The command line.</param>
    private static void Names(Pair panels, CommandBar commandBar)
    {
        foreach (var entry in panels.Active.Targets())
        {
            commandBar.Insert(entry.Name);
        }
    }

    /// <summary>
    /// Calls off whatever is running: a command first, then the file work. A dialog is not here, since the
    /// framework hands every key to the dialog on top before this screen sees any of them.
    /// </summary>
    /// <param name="operations">The file work.</param>
    /// <param name="runner">The commands.</param>
    /// <returns>Nowhere: calling something off never leaves the screen.</returns>
    private static ViewRoute Stop(Operations operations, Runner runner)
    {
        if (runner.IsRunning)
        {
            runner.Stop();
        }
        else
        {
            operations.Cancel();
        }

        return ViewRoute.None;
    }
}
