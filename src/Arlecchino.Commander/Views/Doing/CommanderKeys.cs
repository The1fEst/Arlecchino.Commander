using System;
using System.Collections.Generic;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Commands;
using Arlecchino.Input;
using Arlecchino.Navigation;
using Arlecchino.State;
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
    /// <param name="operations">The file work, which Alt+Esc calls off.</param>
    /// <param name="runner">The commands, which Alt+Esc stops.</param>
    /// <param name="commandBar">The command line, which Alt+Enter writes to.</param>
    /// <param name="state">Where the dialog on top lives.</param>
    /// <param name="lifetime">How the application is quit.</param>
    /// <returns>Every key the screen answers to.</returns>
    public static IReadOnlyList<ViewCommand> For(
        Doings doings,
        Pair panels,
        Sessions sessions,
        Operations operations,
        Runner runner,
        CommandBar commandBar,
        ArlecchinoState state,
        IHostApplicationLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(doings);
        ArgumentNullException.ThrowIfNull(panels);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(lifetime);

        return
        [
            Bind.To(new(ConsoleKey.F2), LocString.TabsTitle, () => TabList.Open(doings)),
            Bind.To(KeyBinding.AltOrSuper(ConsoleKey.F1), LocString.KeyDriveLeft, () => doings.ChooseDrive(panels.Left)),
            Bind.To(KeyBinding.AltOrSuper(ConsoleKey.F2), LocString.KeyDriveRight, () => doings.ChooseDrive(panels.Right)),
            Bind.Going(KeyBinding.AltOrSuper(ConsoleKey.F7), LocString.MenuFindFile, doings.Find),
            Bind.Going(new(ConsoleKey.F3), LocString.View, doings.Read),
            Bind.To(new(ConsoleKey.F4), LocString.Filter, () => doings.Filter(panels.Active)),
            Bind.To(new(ConsoleKey.F5), LocString.Copy, doings.Files.Copy),
            Bind.To(new(ConsoleKey.F6), LocString.Move, doings.Files.Move),
            Bind.To(new(ConsoleKey.F6, KeyModifiers.Shift), LocString.Rename, doings.Files.Rename),
            Bind.To(new(ConsoleKey.F7), LocString.MenuMakeFolder, doings.Files.MakeFolder),
            Bind.To(new(ConsoleKey.F8), LocString.Delete, doings.Files.Delete),
            Bind.To(new(ConsoleKey.F8, KeyModifiers.Shift), LocString.MenuDeleteForGood, doings.Files.DeleteForGood),
            Bind.To(new(ConsoleKey.F9), LocString.Menu, () => Menu.Open(doings)),
            Bind.To(new(ConsoleKey.R, KeyModifiers.Control), LocString.MenuReload, doings.Reload),
            Bind.To(new(ConsoleKey.H, KeyModifiers.Control), LocString.MenuShowHidden, () => doings.ToggleHidden(panels.Active)),
            Bind.To(new(ConsoleKey.U, KeyModifiers.Control), LocString.MenuSwapPanels, doings.Swap),
            Bind.To(KeyBinding.AltOrSuper(ConsoleKey.C), LocString.MenuCopyPaths, doings.CopyPaths),
            Bind.To(new(ConsoleKey.S, KeyModifiers.Control, ConsoleKey.S, KeyModifiers.Alt), LocString.KeySearch, () => panels.Active.Search()),
            Bind.To(new(ConsoleKey.PageUp, KeyModifiers.Control), LocString.KeyFolderAbove, () => panels.Active.Ascend()),
            Bind.To(new(ConsoleKey.PageDown, KeyModifiers.Control), LocString.KeyOpenFolder, () => panels.Active.Descend()),
            Bind.To(KeyBinding.AltOrSuper(ConsoleKey.G), LocString.KeyTop, () => panels.Active.Top()),
            Bind.To(KeyBinding.AltOrSuper(ConsoleKey.R), LocString.KeyMiddle, () => panels.Active.Middle()),
            Bind.To(KeyBinding.AltOrSuper(ConsoleKey.J), LocString.KeyBottom, () => panels.Active.Bottom()),
            Bind.To(KeyBinding.AltOrSuper(ConsoleKey.H), LocString.FoldersBeenIn, () => doings.Places.History(panels.Active)),
            Bind.To(KeyBinding.AltOrSuper(ConsoleKey.Y), LocString.KeyBack, doings.Back),
            Bind.To(KeyBinding.AltOrSuper(ConsoleKey.U), LocString.KeyForward, doings.Forward),
            Bind.To(new(ConsoleKey.B, KeyModifiers.Control, ConsoleKey.Oem5, KeyModifiers.Control), LocString.Hotlist, () => doings.Places.Hotlist(panels.Active)),
            Bind.To(KeyBinding.AltOrSuper(ConsoleKey.I), LocString.MenuBothPanelsHere, () => panels.Passive.GoTo(panels.Active.Folder)),
            Bind.To(KeyBinding.AltOrSuper(ConsoleKey.O), LocString.KeyOtherPanelInto, doings.Beside),
            Bind.To(new(ConsoleKey.K, KeyModifiers.Control), LocString.PaletteTitle, () => Menu.Palette(doings)),
            Bind.To(KeyBinding.AltOrSuper(ConsoleKey.K), LocString.MenuOpenSavedHost, () => doings.Dialling.Saved(panels.Active)),
            Bind.To(KeyBinding.AltOrSuper(ConsoleKey.T), LocString.TabsNew, sessions.Add),
            Bind.When(KeyBinding.AltOrSuper(ConsoleKey.W), LocString.TabsClose, Several(sessions), () => Closed(sessions)),
            Bind.When(KeyBinding.AltOrSuper(ConsoleKey.PageDown), LocString.TabsNext, Several(sessions), () => Stepped(sessions, forward: true)),
            Bind.When(KeyBinding.AltOrSuper(ConsoleKey.PageUp), LocString.TabsPrevious, Several(sessions), () => Stepped(sessions, forward: false)),
            Bind.To(new(ConsoleKey.F10), LocString.BarQuit, lifetime.StopApplication),
            Bind.Going(new(ConsoleKey.O, KeyModifiers.Control), LocString.MenuWhatCommandsSaid, static () => ViewKind.Output),
            Bind.To(KeyBinding.AltOrSuper(ConsoleKey.Enter), LocString.KeyNameOntoLine, () => Named(panels, commandBar)),
            Bind.When(KeyBinding.AltOrSuper(ConsoleKey.Escape), LocString.KeyStop, () => operations.IsBusy || runner.IsRunning, () => Stop(operations, runner)),
        ];
    }

    /// <summary>
    /// The second half of a <c>Ctrl+X</c> pair. These are the operations wanted often enough to have a key
    /// and seldom enough not to have one of their own. A pair leaves the alphabet free for the panels while
    /// still putting them a keystroke and a letter away.
    /// </summary>
    /// <param name="doings">Everything the screen can do.</param>
    /// <param name="commandBar">The command line, which two of the pairs write to.</param>
    /// <param name="state">Where the last word said is kept.</param>
    /// <param name="key">The letter that followed.</param>
    public static void Prefixed(Doings doings, CommandBar commandBar, ArlecchinoState state, KeyPress key)
    {
        ArgumentNullException.ThrowIfNull(doings);
        ArgumentNullException.ThrowIfNull(commandBar);
        ArgumentNullException.ThrowIfNull(state);

        var panel = doings.Panels.Active;

        switch (char.ToLowerInvariant(key.Character))
        {
            case 'c':
                doings.Rights.Mode();
                break;
            case 'o':
                doings.Rights.Owner();
                break;
            case 's':
                doings.Linking.Make(hard: false);
                break;
            case 'l':
                doings.Linking.Make(hard: true);
                break;
            case 'd':
                doings.Compare();
                break;
            case 'p':
                commandBar.Insert(panel.Folder);
                break;
            case 't':
                foreach (var entry in panel.Targets())
                {
                    commandBar.Insert(entry.Name);
                }

                break;
            case 'h':
                doings.Places.Remember(panel.Folder);
                break;
            case 'j':
                doings.Navigation.Apply(Routes.Notifications);
                break;
            default:
                state.Output = Loc(LocString.PrefixHint);
                break;
        }
    }

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

    /// <summary>
    /// Calls off whatever is running: a command first, then the file work. A dialog is not here — the
    /// framework hands every key to the dialog on top before this screen sees any of them.
    ///
    /// It is <c>Alt+Esc</c> and not <c>Esc</c> because plain Escape is already the way out of half a dozen
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
