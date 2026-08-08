using System;
using System.Collections.Generic;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Commands;
using Arlecchino.Input;
using Arlecchino.Navigation;
using Microsoft.Extensions.Hosting;

namespace Arlecchino.Commander.Views.Doing;

/// <summary>
/// Which key does which of the things the screen can do.
///
/// It is a table and nothing else: no key here decides anything, works anything out, or holds a piece
/// of the behavior that the menu entry beside it does not get. Everything a key does is a call to
/// <see cref="Doings"/>, which is what lets the palette list the keys and the menu in one list.
/// </summary>
public static class CommanderKeys
{
    /// <summary>Builds the table.</summary>
    /// <param name="doings">Everything the screen can do.</param>
    /// <param name="panels">The two panels on screen.</param>
    /// <param name="sessions">The tabs, which four of these keys open, close and step between.</param>
    /// <param name="operations">The file work, which the stop key calls off.</param>
    /// <param name="runner">The commands, which the stop key stops.</param>
    /// <param name="commandBar">The command line, which three of these keys write to.</param>
    /// <param name="lifetime">How the application is quit.</param>
    /// <returns>Every key the screen answers to.</returns>
    public static IReadOnlyList<ViewCommand> For(
        Doings doings,
        Pair panels,
        Sessions sessions,
        Operations operations,
        Runner runner,
        CommandBar commandBar,
        IHostApplicationLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(doings);
        ArgumentNullException.ThrowIfNull(panels);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(lifetime);

        return
        [
            Bind.To(new(ConsoleKey.F1, KeyModifiers.Control),
                LocString.KeyDriveLeft,
                () => doings.ChooseDrive(panels.Left)),
            Bind.To(new(ConsoleKey.F2, KeyModifiers.Control),
                LocString.KeyDriveRight,
                () => doings.ChooseDrive(panels.Right)),
            Bind.Going(new(ConsoleKey.F3),
                LocString.View,
                doings.Read),
            Bind.To(new(ConsoleKey.F4),
                LocString.Filter,
                () => doings.Filter(panels.Active)),
            Bind.To(new(ConsoleKey.F5),
                LocString.Copy,
                doings.Files.Copy),
            Bind.To(new(ConsoleKey.F6),
                LocString.Move,
                doings.Files.Move),
            Bind.To(new(ConsoleKey.F6,
                    KeyModifiers.Shift),
                LocString.Rename,
                doings.Files.Rename),
            Bind.To(new(ConsoleKey.F7),
                LocString.MenuMakeFolder,
                doings.Files.MakeFolder),
            Bind.Going(new(ConsoleKey.F7, KeyModifiers.Control),
                LocString.MenuFindFile,
                doings.Find),
            Bind.To(new(ConsoleKey.F8),
                LocString.Delete,
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
            Bind.To(Hotlisting(),
                LocString.Hotlist,
                () => doings.Places.Hotlist(panels.Active)),
            Bind.To(new(ConsoleKey.K, KeyModifiers.Control),
                LocString.PaletteTitle,
                () => Menu.Palette(doings)),
            Bind.Going(new(ConsoleKey.O, KeyModifiers.Control),
                LocString.MenuWhatCommandsSaid,
                static () => ViewKind.Output),

            Bind.To(Go(ConsoleKey.I)
                    .AddAlternative(ConsoleKey.PageUp, KeyModifiers.Control),
                LocString.KeyFolderAbove,
                () => panels.Active.Ascend()),
            Bind.To(Go(ConsoleKey.K)
                    .AddAlternative(ConsoleKey.PageDown, KeyModifiers.Control),
                LocString.KeyOpenFolder,
                () => panels.Active.Descend()),
            Bind.To(Go(ConsoleKey.J),
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
            Bind.To(Go(ConsoleKey.P),
                LocString.FoldersBeenIn,
                () => doings.Places.History(panels.Active)),
            Bind.To(Go(ConsoleKey.H),
                LocString.MenuBothPanelsHere,
                () => panels.Passive.GoTo(panels.Active.Folder)),
            Bind.To(Go(ConsoleKey.Y),
                LocString.KeyOtherPanelInto,
                doings.Beside),
            Bind.To(Go(ConsoleKey.N),
                LocString.MenuOpenSavedHost,
                () => doings.Dialling.Saved(panels.Active)),

            Bind.To(Tab(ConsoleKey.I),
                LocString.TabsNew,
                sessions.Add),
            Bind.When(Tab(ConsoleKey.K),
                LocString.TabsClose,
                Several(sessions),
                () => Closed(sessions)),
            Bind.When(Tab(ConsoleKey.L),
                LocString.TabsNext,
                Several(sessions),
                () => Stepped(sessions, forward: true)),
            Bind.When(Tab(ConsoleKey.J),
                LocString.TabsPrevious,
                Several(sessions),
                () => Stepped(sessions, forward: false)),
            Bind.To(Tab(ConsoleKey.O)
                    .AddAlternative(ConsoleKey.F2),
                LocString.TabsTitle,
                () => TabList.Open(doings)),

            Bind.To(Execute(ConsoleKey.C),
                LocString.Permissions,
                doings.Rights.Mode),
            Bind.To(Execute(ConsoleKey.O),
                LocString.MenuOwner,
                doings.Rights.Owner),
            Bind.To(Execute(ConsoleKey.S),
                LocString.MenuSymbolicLink,
                () => doings.Linking.Make(hard: false)),
            Bind.To(Execute(ConsoleKey.L),
                LocString.MenuHardLink,
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
            Bind.To(Execute(ConsoleKey.H),
                LocString.HotlistAdd,
                () => doings.Places.Remember(panels.Active.Folder)),
            Bind.To(Execute(ConsoleKey.Y),
                LocString.MenuCopyPaths,
                doings.CopyPaths),
            Bind.Going(Execute(ConsoleKey.J),
                LocString.MenuNotifications,
                static () => Routes.Notifications),
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
    /// A key behind the <c>t</c> leader, which is the one the tabs live behind. They were spread between
    /// the function keys, a pair of Control letters and the leader that takes you somewhere, which is
    /// three places to look for five keys that belong together.
    /// </summary>
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
    /// A key behind the <c>x</c> leader, which is the one that does something to what the panel is
    /// showing. These are the operations wanted often enough to have a key and seldom enough not to have
    /// one of their own, and they are the letters Midnight Commander puts behind its own <c>Ctrl+X</c>.
    /// </summary>
    /// <param name="key">The key that finishes it.</param>
    /// <returns>The chord.</returns>
    private static KeyBinding Execute(ConsoleKey key) => new KeyBinding(ConsoleKey.X).ThenKey(key);

    /// <summary>
    /// Search as you type. The slash is what asks for it, since the letters that follow are the search
    /// and not the keys — the same trade the colon makes for the command line. Control and Alt still
    /// answer for the hands that learned them on Midnight Commander.
    /// </summary>
    /// <returns>The binding.</returns>
    private static KeyBinding Searching() =>
        new KeyBinding(ConsoleKey.Oem2)
            .AddAlternative(ConsoleKey.S, KeyModifiers.Control)
            .AddAlternative(ConsoleKey.S, KeyModifiers.Alt);

    /// <summary>The hotlist, which answers to the backslash beside it as Midnight Commander does.</summary>
    /// <returns>The binding.</returns>
    private static KeyBinding Hotlisting() =>
        new KeyBinding(ConsoleKey.B, KeyModifiers.Control).AddAlternative(ConsoleKey.Oem5, KeyModifiers.Control);

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
    /// Calls off whatever is running: a command first, then the file work. A dialog is not here — the
    /// framework hands every key to the dialog on top before this screen sees any of them.
    ///
    /// It is a chord and not <c>Esc</c> because plain Escape is already the way out of half a dozen
    /// things: a search being typed, a filter, a dialog, the screen you are on. A key that means "get out of
    /// this" some of the time and "stop the copy" the rest of the time is one you hesitate over, and stopping
    /// work that is running deserves a key nothing else is asking for.
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
