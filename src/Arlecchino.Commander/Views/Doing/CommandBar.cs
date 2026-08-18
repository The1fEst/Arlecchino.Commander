using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Atoms.Local;
using Arlecchino.Editing;
using Arlecchino.Commander.Files.Work;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Rendering;
using Arlecchino.State;

namespace Arlecchino.Commander.Views.Doing;

/// <summary>
/// The one row between the panels and the bar of keys, where a command is typed and from where it is run.
/// It takes a key only once the colon has asked, and Escape gives the keyboard back.
/// </summary>
public sealed class CommandBar
{
    private const int PromptRoom = 28;

    private readonly CommandLine _line;
    private readonly Runner _runner;
    private readonly ArlecchinoState _state;
    private readonly ArlecchinoKeymap _keymap;
    private readonly Pair _panels;
    private readonly TextCompleter _completer;

    /// <summary>Puts the line under a pair of panels.</summary>
    /// <param name="line">The line itself.</param>
    /// <param name="runner">What runs what is typed.</param>
    /// <param name="state">Where the last word said is kept.</param>
    /// <param name="keymap">Keys to obey.</param>
    /// <param name="panels">The two panels on screen.</param>
    public CommandBar(CommandLine line, Runner runner, ArlecchinoState state, ArlecchinoKeymap keymap, Pair panels)
    {
        _line = line;
        _runner = runner;
        _state = state;
        _keymap = keymap;
        _panels = panels;
        _completer = new(line.Entry, new CommandWords(panels, runner.History), new ShellWords(), keymap);
    }

    /// <summary>
    /// Draws it, prompted with where a command would run. The path is shortened the way the panel above
    /// shortens it, so the two say the same thing about the same folder.
    /// </summary>
    /// <param name="line">The row to draw on.</param>
    public void Draw(SurfaceRegion line)
    {
        var panel = _panels.Active;
        var place = Paths.Shortened(panel.Source, panel.Folder, PromptRoom);

        _line.Draw(
            line,
            panel.Source.IsRemote ? $"{panel.Source.Label}:{place}" : place,
            Loc(_line.IsTyping ? LocString.CommandLineTail : LocString.CommandLineAsleep));
    }

    /// <summary>Whether the line has the keyboard, which is what the panel asks before reading a letter.</summary>
    public bool IsTyping => _line.IsTyping;

    /// <summary>How many rows a long command carried the line onto, which the screen leaves room for.</summary>
    public LocalAtom<int> Height => _line.Height;

    /// <summary>Asks for the line, as the colon does by itself and as the key of that name does.</summary>
    public void Open() => _line.Open();

    /// <summary>
    /// Draws what the half-typed word could still turn into, which stands over the panels. Nothing is drawn
    /// until Tab has been pressed, and what was found is dropped again by the next letter typed.
    /// </summary>
    /// <param name="region">The room above the foot.</param>
    public void DrawHints(SurfaceRegion region)
    {
        var words = _completer.Words;
        var rows = new List<HintRow>(words.Count);

        foreach (var word in words)
        {
            rows.Add(new(word, "", ""));
        }

        HintRows.Draw(region, Loc(LocString.MenuCommand), rows, _completer.ChosenIndex);
    }

    /// <summary>
    /// Gives the key to the line, which takes it only once it has been asked for. Tab finishes the word
    /// being typed and is offered before the line, so that nothing else claims it.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when the line took it.</returns>
    public bool Handle(KeyPress key)
    {
        if (_line.Opens(key))
        {
            _line.Open();

            return true;
        }

        if (_line.IsTyping && _completer.Handle(key))
        {
            return true;
        }

        if (!_line.IsTyping || _line.IsEmpty || !_keymap.Confirm.Matches(key))
        {
            return _line.Handle(key);
        }

        Enter();

        return true;
    }

    /// <summary>Puts something on the line, as the Ctrl+X pairs and Alt+Enter do.</summary>
    /// <param name="text">What to put there.</param>
    public void Insert(string text) => _line.Insert(text);

    /// <summary>
    /// Puts pasted text on the line, which wakes for it the way it does for the Ctrl+X pairs. It is not
    /// run: a paste ends where the clipboard ends, and Enter is still the key that says go.
    /// </summary>
    /// <param name="text">What was pasted.</param>
    public void Paste(string text) => _line.Paste(text);

    /// <summary>
    /// Runs what is on the line where the panel is looking, with <c>%s</c> and its fellows filled in first.
    /// A <c>cd</c> is decided on what was typed and moves the panel rather than reaching a shell.
    /// </summary>
    private void Enter()
    {
        var command = _line.Take();
        var panel = _panels.Active;

        if (command.Length == 0 || Chdir(panel, command))
        {
            return;
        }

        var cells = Placeholders.Expand(
            command,
            panel.Source,
            panel.Folder,
            panel.Targets(),
            panel.Current);

        _runner.Run(cells, panel.Folder, panel.Source, panel.Reload);
    }

    /// <summary>Moves the panel when what was typed was a <c>cd</c>.</summary>
    /// <param name="panel">The panel.</param>
    /// <param name="command">What was typed.</param>
    /// <returns><c>true</c> when it was a <c>cd</c> and has been dealt with.</returns>
    private bool Chdir(FilePanel panel, string command)
    {
        if (command != "cd" && !command.StartsWith("cd ", StringComparison.Ordinal))
        {
            return false;
        }

        var argument = command.Length > 3 ? command[3..].Trim().Trim('"') : "";

        Answers.From(() => Where(panel, argument),
            place =>
            {
                if (place is null)
                {
                    _state.Output = Loc(LocString.SaidNoFolder, argument);

                    return;
                }

                panel.GoTo(place);
            });

        return true;
    }

    /// <summary>
    /// Works out what a typed <c>cd</c> meant, asking the source whether the name is a folder of its
    /// own or one below the panel. Both questions are round trips on a server.
    /// </summary>
    /// <param name="panel">The panel being moved.</param>
    /// <param name="argument">What was typed after the command.</param>
    /// <returns>Where to go, or <c>null</c> when there is no such folder.</returns>
    private static async Task<string?> Where(FilePanel panel, string argument)
    {
        var place = argument switch
        {
            "" or "~" => panel.Source.Home,
            ".." => panel.Source.Parent(panel.Folder) ?? panel.Folder,
            _ => await panel.Source.FolderExistsAsync(argument, CancellationToken.None).ConfigureAwait(false)
                ? argument
                : panel.Source.Combine(panel.Folder, argument),
        };

        return await panel.Source.FolderExistsAsync(place, CancellationToken.None).ConfigureAwait(false)
            ? place
            : null;
    }
}
