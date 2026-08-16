using System;
using System.Collections.Generic;
using System.Linq;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Views.Doing;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Commands;
using Arlecchino.Input;
using Microsoft.Extensions.Hosting;

namespace Arlecchino.Commander.Views.Commanding;

/// <summary>
///     Every key the screen answers to, and which of them it answers to at this moment: while something is
///     being typed into, the letters belong to whatever is being typed into and not to the screen.
/// </summary>
public sealed class CommanderCommands
{
    private readonly CommanderFooter _footer;
    private readonly Pair _panels;
    private readonly Keyboard _keyboard;
    private readonly IReadOnlyList<ViewCommand> _typed;

    /// <summary>Builds the table and the shorter one taken from it.</summary>
    /// <param name="doings">Everything the screen can do, which every key is a call to.</param>
    /// <param name="panels">The two panels on screen.</param>
    /// <param name="sessions">The tabs, which four of these keys open, close and step between.</param>
    /// <param name="operations">The file work, which the stop key calls off.</param>
    /// <param name="runner">The commands, which the stop key stops.</param>
    /// <param name="footer">The two lines, which some of these keys write to or open.</param>
    /// <param name="lifetime">How the application is quit.</param>
    /// <param name="keyboard">Where the table is left for the screen that lists every key.</param>
    public CommanderCommands(
        Doings doings,
        Pair panels,
        Sessions sessions,
        Operations operations,
        Runner runner,
        CommanderFooter footer,
        IHostApplicationLifetime lifetime,
        Keyboard keyboard)
    {
        _panels = panels;
        _footer = footer;
        _keyboard = keyboard;

        _keyboard.Keys = CommanderKeys.For(
            doings,
            panels,
            sessions,
            operations,
            runner,
            footer.Line,
            footer.Setting,
            lifetime);

        _typed = [.. _keyboard.Keys.Where(command => !WantedByTheLine(command.Binding))];
    }

    /// <summary>
    ///     The keys to match against now: everything, or everything minus the ones a letter could be while
    ///     something is being typed into. What is left then is the function keys and everything held with a
    ///     modifier.
    /// </summary>
    /// <returns>The commands to match this key against.</returns>
    public IReadOnlyList<ViewCommand> Now()
    {
        return _footer.IsTyping || _panels.Active.IsSearching ? _typed : _keyboard.Keys;
    }

    /// <summary>
    ///     Whether a line of text would rather have this key. Anything pressed on its own it would: the
    ///     letters it types, and the arrows and the rub-outs it edits with.
    /// </summary>
    /// <param name="binding">The binding to judge.</param>
    /// <returns><c>true</c> when the line should be given the key instead.</returns>
    private static bool WantedByTheLine(KeyBinding binding)
    {
        return binding is { Modifiers: KeyModifiers.None, Key: < ConsoleKey.F1 or > ConsoleKey.F24 };
    }
}
