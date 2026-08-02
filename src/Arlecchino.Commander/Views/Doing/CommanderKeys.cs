using System;
using System.Collections.Generic;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Panel;
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
/// of the behaviour that the menu entry beside it does not get. Everything a key does is a call to
/// <see cref="Doings"/>, which is what lets the palette list the keys and the menu in one list.
/// </summary>
public static class CommanderKeys
{
    /// <summary>Builds the table.</summary>
    /// <param name="doings">Everything the screen can do.</param>
    /// <param name="panels">The two panels on screen.</param>
    /// <param name="operations">The file work, which Escape calls off.</param>
    /// <param name="runner">The commands, which Escape stops.</param>
    /// <param name="typing">The command line, which Alt+Enter writes to.</param>
    /// <param name="state">Where the dialog on top lives.</param>
    /// <param name="lifetime">How the application is quit.</param>
    /// <returns>Every key the screen answers to.</returns>
    public static IReadOnlyList<ViewCommand> For(
        Doings doings,
        Pair panels,
        Operations operations,
        Runner runner,
        Typing typing,
        ArlecchinoState state,
        IHostApplicationLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(doings);
        ArgumentNullException.ThrowIfNull(panels);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(lifetime);

        return
        [
            ViewCommand.For(ConsoleKey.F2, static () => "user menu", doings.OpenUserMenu),
            ViewCommand.For(new KeyBinding(ConsoleKey.F1, ConsoleModifiers.Alt), static () => "drive on the left",
                () => doings.ChooseDrive(panels.Left)),
            ViewCommand.For(new KeyBinding(ConsoleKey.F2, ConsoleModifiers.Alt), static () => "drive on the right",
                () => doings.ChooseDrive(panels.Right)),
            new()
            {
                Binding = new(ConsoleKey.F7, ConsoleModifiers.Alt),
                Label = static () => "find file",
                Run = doings.Find,
            },
            ViewCommand.Navigating(ConsoleKey.F3, static () => "view", doings.Read),
            ViewCommand.For(ConsoleKey.F4, static () => "filter", () => doings.Filter(panels.Active)),
            ViewCommand.For(ConsoleKey.F5, static () => "copy", doings.Files.Copy),
            ViewCommand.For(ConsoleKey.F6, static () => "move", doings.Files.Move),
            ViewCommand.For(new KeyBinding(ConsoleKey.F6, ConsoleModifiers.Shift), static () => "rename",
                doings.Files.Rename),
            ViewCommand.For(ConsoleKey.F7, static () => "make folder", doings.Files.MakeFolder),
            ViewCommand.For(ConsoleKey.F8, static () => "delete", doings.Files.Delete),
            ViewCommand.For(ConsoleKey.F9, static () => "menu", () => Menu.Open(doings)),
            ViewCommand.For(new KeyBinding(ConsoleKey.R, ConsoleModifiers.Control), static () => "reload",
                doings.Reload),
            ViewCommand.For(new KeyBinding(ConsoleKey.H, ConsoleModifiers.Control), static () => "hidden files",
                () => doings.ToggleHidden(panels.Active)),
            ViewCommand.For(new KeyBinding(ConsoleKey.U, ConsoleModifiers.Control), static () => "swap panels",
                doings.Swap),
            ViewCommand.For(
                new KeyBinding(ConsoleKey.S, ConsoleModifiers.Control, ConsoleKey.S, ConsoleModifiers.Alt),
                static () => "search as you type", () => panels.Active.Search()),
            ViewCommand.For(new KeyBinding(ConsoleKey.PageUp, ConsoleModifiers.Control), static () => "folder above",
                () => panels.Active.Ascend()),
            ViewCommand.For(new KeyBinding(ConsoleKey.PageDown, ConsoleModifiers.Control),
                static () => "open folder", () => panels.Active.Descend()),
            ViewCommand.For(new KeyBinding(ConsoleKey.G, ConsoleModifiers.Alt), static () => "top",
                () => panels.Active.Top()),
            ViewCommand.For(new KeyBinding(ConsoleKey.R, ConsoleModifiers.Alt), static () => "middle",
                () => panels.Active.Middle()),
            ViewCommand.For(new KeyBinding(ConsoleKey.J, ConsoleModifiers.Alt), static () => "bottom",
                () => panels.Active.Bottom()),
            ViewCommand.For(new KeyBinding(ConsoleKey.H, ConsoleModifiers.Alt), static () => "folders been in",
                () => doings.Places.History(panels.Active)),
            ViewCommand.For(new KeyBinding(ConsoleKey.Y, ConsoleModifiers.Alt), static () => "back", doings.Back),
            ViewCommand.For(new KeyBinding(ConsoleKey.U, ConsoleModifiers.Alt), static () => "forward",
                doings.Forward),
            ViewCommand.For(new KeyBinding(ConsoleKey.B, ConsoleModifiers.Control, ConsoleKey.Oem5,
                ConsoleModifiers.Control), static () => "hotlist", () => doings.Places.Hotlist(panels.Active)),
            ViewCommand.For(new KeyBinding(ConsoleKey.I, ConsoleModifiers.Alt), static () => "both panels here",
                () => panels.Passive.GoTo(panels.Active.Folder)),
            ViewCommand.For(new KeyBinding(ConsoleKey.O, ConsoleModifiers.Alt),
                static () => "other panel into folder", doings.Beside),
            ViewCommand.For(new KeyBinding(ConsoleKey.K, ConsoleModifiers.Control), static () => "do anything",
                () => Menu.Palette(doings)),
            ViewCommand.For(new KeyBinding(ConsoleKey.K, ConsoleModifiers.Alt), static () => "open a saved host",
                () => doings.Dialling.Saved(panels.Active)),
            ViewCommand.For(ConsoleKey.F10, static () => "quit", lifetime.StopApplication),
            new()
            {
                Binding = new(ConsoleKey.O, ConsoleModifiers.Control),
                Label = static () => "what the commands said",
                Run = static () => ViewKind.Output,
            },
            ViewCommand.For(new KeyBinding(ConsoleKey.Enter, ConsoleModifiers.Alt),
                static () => "name onto the command line", () => Named(panels, typing)),
            new()
            {
                Binding = new(ConsoleKey.Escape),
                Label = () => state.Modal is null ? "stop what is running" : "call it off",
                IsEnabled = () => state.Modal is not null || operations.IsBusy || runner.IsRunning,
                Run = () => Stop(state, operations, runner),
            },
        ];
    }

    /// <summary>
    /// The second half of a <c>Ctrl+X</c> pair. These are the operations that are wanted often enough
    /// to have a key and not often enough to have one of their own; a pair leaves the alphabet free
    /// for the panels while still putting them a keystroke and a letter away.
    /// </summary>
    /// <param name="doings">Everything the screen can do.</param>
    /// <param name="typing">The command line, which two of the pairs write to.</param>
    /// <param name="state">Where the last word said is kept.</param>
    /// <param name="key">The letter that followed.</param>
    public static void Prefixed(Doings doings, Typing typing, ArlecchinoState state, ConsoleKeyInfo key)
    {
        ArgumentNullException.ThrowIfNull(doings);
        ArgumentNullException.ThrowIfNull(typing);
        ArgumentNullException.ThrowIfNull(state);

        var panel = doings.Panels.Active;

        switch (char.ToLowerInvariant(key.KeyChar))
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
                typing.Insert(panel.Folder);
                break;
            case 't':
                foreach (var entry in panel.Targets())
                {
                    typing.Insert(entry.Name);
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

    /// <summary>Puts the name under the cursor on the command line.</summary>
    /// <param name="panels">The two panels on screen.</param>
    /// <param name="typing">The command line.</param>
    private static void Named(Pair panels, Typing typing)
    {
        if (panels.Active.Current is { IsParent: false } current)
        {
            typing.Insert(current.Name);
        }
    }

    /// <summary>
    /// Calls off whatever is on: the dialog first, then a command, then the file work. One key for
    /// all three, because "get me out of this" is one thought however many things are going on.
    /// </summary>
    /// <param name="state">Where the dialog on top lives.</param>
    /// <param name="operations">The file work.</param>
    /// <param name="runner">The commands.</param>
    /// <returns>Nowhere: calling something off never leaves the screen.</returns>
    private static ViewRoute Stop(ArlecchinoState state, Operations operations, Runner runner)
    {
        if (state.Modal is not null)
        {
            state.CloseModal();
        }
        else if (runner.IsRunning)
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
