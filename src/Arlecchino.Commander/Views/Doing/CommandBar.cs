using System;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Work;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Hosting;
using Arlecchino.Rendering;
using Arlecchino.State;

namespace Arlecchino.Commander.Views.Doing;

/// <summary>
/// The line a command is typed on, and what happens when it is entered. It is the one row between the
/// panels and the bar of keys, and it is always that one row however tall the bar below it has grown.
///
/// The line takes a key only while there is something typed on it: an empty line leaves Space, Enter,
/// Backspace and the marking keys to the panel, which is what makes a command line and a file list
/// able to share one keyboard without a mode switch between them.
/// </summary>
public sealed class CommandBar
{
    private const int PromptRoom = 28;

    private readonly CommandLine _line;
    private readonly Runner _runner;
    private readonly ArlecchinoState _state;
    private readonly ArlecchinoKeymap _keymap;
    private readonly Pair _panels;

    /// <summary>Sets the line up under a pair of panels.</summary>
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
    }

    /// <summary>
    /// Draws it, prompted with where a command would run. The path is shortened the way the panel
    /// above shortens it — the home folder as a tilde, and a head too long for the room cut off — so
    /// the two say the same thing about the same folder.
    /// </summary>
    /// <param name="line">The row to draw on.</param>
    public void Draw(SurfaceRegion line)
    {
        var panel = _panels.Active;
        var where = Paths.Shortened(panel.Source, panel.Folder, PromptRoom);

        _line.Draw(line, panel.Source.IsRemote ? $"{panel.Source.Label}:{where}" : where);
    }

    /// <summary>Gives the key to the line, which takes it only when there is something typed.</summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when the line took it.</returns>
    public bool Handle(ConsoleKeyInfo key)
    {
        if (_line.IsEmpty || !_keymap.Confirm.Matches(key))
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
    /// Runs what is on the line where the panel is looking. A <c>cd</c> is not run at all: it moves
    /// the panel, because a shell started for one command would forget it the moment it ended.
    ///
    /// What the panel is showing is filled in first, so <c>%s</c> and its fellows name the marked files
    /// rather than reaching the shell as themselves. The <c>cd</c> is decided on what was typed, not on
    /// what it expanded to — the point is to spot the word, and expanding first would only put paths in
    /// front of it.
    /// </summary>
    private void Enter()
    {
        var command = _line.Take();
        var panel = _panels.Active;

        if (command.Length == 0 || Chdir(panel, command))
        {
            return;
        }

        var filled = Placeholders.Expand(
            command,
            panel.Source,
            panel.Folder,
            panel.Targets(),
            panel.Current);

        _runner.Run(filled, panel.Folder, panel.Source, panel.Reload);
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

        var wanted = command.Length > 3 ? command[3..].Trim().Trim('"') : "";

        Answers.From(() => Where(panel, wanted),
            where =>
            {
                if (where is null)
                {
                    _state.Output = Loc(LocString.SaidNoFolder, wanted);

                    return;
                }

                panel.GoTo(where);
            });

        return true;
    }

    /// <summary>
    /// Works out what a typed <c>cd</c> meant, asking the source whether the name is a folder of its
    /// own or one below the panel. Both questions are round trips on a server.
    /// </summary>
    /// <param name="panel">The panel being moved.</param>
    /// <param name="wanted">What was typed after the command.</param>
    /// <returns>Where to go, or <c>null</c> when there is no such folder.</returns>
    private static async Task<string?> Where(FilePanel panel, string wanted)
    {
        var where = wanted switch
        {
            "" or "~" => panel.Source.Home,
            ".." => panel.Source.Parent(panel.Folder) ?? panel.Folder,
            _ => await panel.Source.FolderExistsAsync(wanted, CancellationToken.None).ConfigureAwait(false)
                ? wanted
                : panel.Source.Combine(panel.Folder, wanted),
        };

        return await panel.Source.FolderExistsAsync(where, CancellationToken.None).ConfigureAwait(false)
            ? where
            : null;
    }
}
