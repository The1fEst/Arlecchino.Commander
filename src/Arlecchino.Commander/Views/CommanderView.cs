using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets;
using Arlecchino.Commands;
using Arlecchino.Focus;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Layout;
using Arlecchino.Navigation;
using Arlecchino.Rendering;
using Arlecchino.State;
using Arlecchino.Widgets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static Arlecchino.Layout.PaneSplit;
using static Arlecchino.Layout.PaneTree;

namespace Arlecchino.Commander.Views;

public sealed class CommanderView : IArlecchinoView
{
    private const int FooterRows = 2;
    private const int HeaderRows = 1;
    private const int TabRow = 0;
    private const int GutterWidth = 3;
    private const int PromptRoom = 28;
    private const int BarCells = 22;
    private const int CardWidth = 34;
    private const int SideRoom = 2;
    private const int Fresh = -1;
    private const string StopsHint = "Esc stops";
    private const string PrefixHint =
        "Ctrl+X · c permissions, o owner, s symlink, l hard link, d compare, p path, t names, h hotlist, j jobs";

    private const int SameSecond = 2;
    private const string AddHot = "Add this folder";
    private const string DropHot = "Forget a folder";

    private static readonly string[] PanelItems =
    [
        "Find file",
        "Sort by name",
        "Sort by size",
        "Sort by date",
        "Show hidden files",
        "Choose drive",
        "Open a saved host",
        "Connect to a server",
        "Disconnect",
        "Reload",
    ];

    private static readonly string[] FileItems =
    [
        "View",
        "Copy",
        "Move",
        "Rename",
        "Make folder",
        "Permissions",
        "Owner",
        "Symbolic link",
        "Hard link",
        "Delete",
    ];

    private static readonly string[] CommandItems =
    [
        "Swap panels",
        "Both panels here",
        "Compare directories",
        "Folders been in",
        "Hotlist",
        "Mark a group",
        "Unmark a group",
        "Invert the marks",
        "Filter",
        "Run a command over SSH",
        "What the commands said",
        "Reload both panels",
    ];

    private static readonly string[] OptionItems =
    [
        "Hidden files here",
        "Hidden files there",
        "Notifications",
        "Keys",
    ];

    private static readonly MenuSection[] Sections =
    [
        new("Left", PanelItems),
        new("File", FileItems),
        new("Command", CommandItems),
        new("Options", OptionItems),
        new("Right", PanelItems),
    ];

    private readonly Surface _surface;
    private readonly Panels _panels;
    private readonly Remote _remote;
    private readonly ArlecchinoState _state;
    private readonly IServiceProvider _services;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly Dictionary<Session, (FilePanel Left, FilePanel Right)> _panes = [];
    private readonly List<(int Column, int Width, int Index)> _tabs = [];
    private readonly Operations _operations;
    private readonly Spinner _spinner = new();

    private readonly ProgressBar _bar = new()
    {
        Caption = static value => $"{value:0}%",
    };
    private readonly Runner _runner;
    private readonly Finder _finder;
    private readonly CommandLine _line;
    private readonly ArlecchinoKeymap _keymap;
    private readonly KeyText _keys;

    private FilePanel _left;
    private FilePanel _right;
    private FocusRing _focus;
    private PaneTree _layout;
    private Session _showing;
    private int _seen;
    private int _moved;
    private bool _prefix;

    public CommanderView(
        Surface surface,
        Panels panels,
        Remote remote,
        Operations operations,
        Runner runner,
        Finder finder,
        ArlecchinoState state,
        ArlecchinoOptions options,
        IServiceProvider services,
        IHostApplicationLifetime lifetime)
    {
        _surface = surface;
        _panels = panels;
        _remote = remote;
        _operations = operations;
        _runner = runner;
        _finder = finder;
        _state = state;
        _services = services;
        _lifetime = lifetime;
        _keymap = options.Keymap;

        _keys = KeyText.For(options.TextInput);
        _line = new(runner.History, _keys, options.Keymap);
        _seen = operations.Revision.Value;
        _moved = panels.Revision.Value;
        _showing = panels.Current;

        (_left, _right) = Panes(_showing);
        _layout = Lay();
        _focus = _layout.AsFocusRing(options.Keymap);

        _focus.Focus(panels.RightIsActive.Value ? _right : _left);
    }

    /// <summary>
    /// The two panels of a session, made once and kept. A tab that is come back to shows what it showed
    /// before — the same cursor, the same place in a long folder — which it could not do if its panels
    /// were built afresh every time it was switched to.
    /// </summary>
    /// <param name="session">Whose panels.</param>
    /// <returns>The pair.</returns>
    private (FilePanel Left, FilePanel Right) Panes(Session session)
    {
        if (_panes.TryGetValue(session, out var made))
        {
            return made;
        }

        FilePanel Over(PanelState state) => new(state, _keymap, _keys) { OnOpenFile = Open, OnGroup = Group };

        made = (Over(session.Left), Over(session.Right));
        _panes[session] = made;

        return made;
    }

    private PaneTree Lay() => Branch(
        Rows,
        PaneSize.Cells(HeaderRows),
        Leaf(DrawHeader),
        Branch(
            Rows,
            PaneSize.CellsFromEnd(FooterRows),
            Branch(
                Columns,
                PaneSize.Fraction(0.5),
                Leaf(_left),
                Branch(Columns, PaneSize.Cells(GutterWidth), Leaf(DrawGutter), Leaf(_right))),
            Branch(Rows, 1, Leaf(DrawCommandLine), Leaf(DrawActions))));

    /// <summary>
    /// Swaps the panels over when the tab has changed. The layout holds the two it was built with, so a
    /// new tab means a new layout and a focus ring to go with it.
    /// </summary>
    private void Showing()
    {
        if (ReferenceEquals(_showing, _panels.Current))
        {
            return;
        }

        _showing = _panels.Current;
        (_left, _right) = Panes(_showing);
        _layout = Lay();
        _focus = _layout.AsFocusRing(_keymap);

        _focus.Focus(_panels.RightIsActive.Value ? _right : _left);
    }

    /// <summary>
    /// Draws the screen, reloading the panels first when work that was running elsewhere has finished
    /// since the last frame — the operation outlives this screen, so the screen catches up rather than
    /// being told.
    /// </summary>
    public void Draw()
    {
        Showing();

        if (_seen != _operations.Revision.Value)
        {
            _seen = _operations.Revision.Value;

            Active().State.Marks.Clear();
            _left.Reload();
            _right.Reload();
        }

        if (_moved != _panels.Revision.Value)
        {
            _moved = _panels.Revision.Value;

            _left.Reload();
            _right.Reload();
        }

        _layout.Draw(_surface.Content);
        DrawJob(_surface.Content);
    }

    /// <summary>
    /// Keys the screen itself takes before the panels see them: the second half of a <c>Ctrl+X</c>
    /// pair, and everything the command line claims while there is something typed on it.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns>Where to go, which is nowhere for all of these.</returns>
    public ViewRoute Handle(ConsoleKeyInfo key)
    {
        if (_prefix)
        {
            _prefix = false;

            Prefixed(key);

            return ViewRoute.None;
        }

        if (key is { Modifiers: ConsoleModifiers.Control, Key: ConsoleKey.X })
        {
            _prefix = true;
            _state.Output = PrefixHint;

            return ViewRoute.None;
        }

        if (key.Modifiers == ConsoleModifiers.Control && key.Key is ConsoleKey.PageDown or ConsoleKey.PageUp)
        {
            _panels.Step(key.Key == ConsoleKey.PageDown);

            return ViewRoute.None;
        }

        if (!Active().IsSearching && Typed(key))
        {
            return ViewRoute.None;
        }

        var route = _focus.Handle(key);

        _panels.RightIsActive.Value = _right.IsFocused;

        return route;
    }

    /// <summary>
    /// Clicks. A click on a tab shows it, which is the one thing on this screen a mouse is better at
    /// than the keyboard; everything else goes to whichever panel was clicked in.
    /// </summary>
    /// <param name="mouse">The event that arrived.</param>
    /// <returns>Where to go, which is nowhere.</returns>
    public ViewRoute HandleMouse(MouseEvent mouse)
    {
        if (mouse.Action == MouseAction.Pressed && mouse.Row == TabRow && Tab(mouse.Column) is { } index)
        {
            if (index == Fresh)
            {
                _panels.Add();
            }
            else
            {
                _panels.Show(index);
            }

            return ViewRoute.None;
        }

        var route = _focus.HandleMouse(mouse);

        _panels.RightIsActive.Value = _right.IsFocused;

        return route;
    }

    /// <summary>Which tab a click landed on.</summary>
    /// <param name="column">How far along the row it was.</param>
    /// <returns>The session it belongs to, or nothing when it hit the gap between two.</returns>
    private int? Tab(int column)
    {
        foreach (var (at, width, index) in _tabs)
        {
            if (column >= at && column < at + width)
            {
                return index;
            }
        }

        return null;
    }

    public IReadOnlyList<ViewCommand> Commands() =>
    [
        ViewCommand.For(ConsoleKey.F2, static () => "user menu", OpenUserMenu),
        ViewCommand.For(new KeyBinding(ConsoleKey.F1, ConsoleModifiers.Alt), static () => "drive on the left",
            () => ChooseDrive(_left)),
        ViewCommand.For(new KeyBinding(ConsoleKey.F2, ConsoleModifiers.Alt), static () => "drive on the right",
            () => ChooseDrive(_right)),
        new()
        {
            Binding = new(ConsoleKey.F7, ConsoleModifiers.Alt),
            Label = static () => "find file",
            Run = Find,
        },
        ViewCommand.Navigating(ConsoleKey.F3, static () => "view", View),
        ViewCommand.For(ConsoleKey.F4, static () => "filter", Filter),
        ViewCommand.For(ConsoleKey.F5, static () => "copy", Copy),
        ViewCommand.For(ConsoleKey.F6, static () => "move", Move),
        ViewCommand.For(new KeyBinding(ConsoleKey.F6, ConsoleModifiers.Shift), static () => "rename", Rename),
        ViewCommand.For(ConsoleKey.F7, static () => "make folder", MakeFolder),
        ViewCommand.For(ConsoleKey.F8, static () => "delete", Delete),
        ViewCommand.For(ConsoleKey.F9, static () => "menu", OpenMenu),
        ViewCommand.For(new KeyBinding(ConsoleKey.R, ConsoleModifiers.Control), static () => "reload", Reload),
        ViewCommand.For(new KeyBinding(ConsoleKey.H, ConsoleModifiers.Control), static () => "hidden files",
            ToggleHidden),
        ViewCommand.For(new KeyBinding(ConsoleKey.U, ConsoleModifiers.Control), static () => "swap panels", Swap),
        ViewCommand.For(new KeyBinding(ConsoleKey.S, ConsoleModifiers.Control, ConsoleKey.S, ConsoleModifiers.Alt),
            static () => "search as you type", () => Active().Search()),
        ViewCommand.For(new KeyBinding(ConsoleKey.PageUp, ConsoleModifiers.Control), static () => "folder above",
            () => Active().Ascend()),
        ViewCommand.For(new KeyBinding(ConsoleKey.PageDown, ConsoleModifiers.Control), static () => "open folder",
            () => Active().Descend()),
        ViewCommand.For(new KeyBinding(ConsoleKey.G, ConsoleModifiers.Alt), static () => "top", () => Active().Top()),
        ViewCommand.For(new KeyBinding(ConsoleKey.R, ConsoleModifiers.Alt), static () => "middle",
            () => Active().Middle()),
        ViewCommand.For(new KeyBinding(ConsoleKey.J, ConsoleModifiers.Alt), static () => "bottom",
            () => Active().Bottom()),
        ViewCommand.For(new KeyBinding(ConsoleKey.H, ConsoleModifiers.Alt), static () => "folders been in",
            () => OpenHistory(Active())),
        ViewCommand.For(new KeyBinding(ConsoleKey.Y, ConsoleModifiers.Alt), static () => "back", Back),
        ViewCommand.For(new KeyBinding(ConsoleKey.U, ConsoleModifiers.Alt), static () => "forward", Forward),
        ViewCommand.For(new KeyBinding(ConsoleKey.B, ConsoleModifiers.Control, ConsoleKey.Oem5,
            ConsoleModifiers.Control), static () => "hotlist", OpenHotlist),
        ViewCommand.For(new KeyBinding(ConsoleKey.I, ConsoleModifiers.Alt), static () => "both panels here",
            () => Passive().GoTo(Active().Folder)),
        ViewCommand.For(new KeyBinding(ConsoleKey.O, ConsoleModifiers.Alt), static () => "other panel into folder",
            Beside),
        ViewCommand.For(new KeyBinding(ConsoleKey.K, ConsoleModifiers.Control), static () => "open a saved host",
            () => OpenSaved(Active())),
        ViewCommand.For(ConsoleKey.F10, static () => "quit", _lifetime.StopApplication),
        new()
        {
            Binding = new(ConsoleKey.O, ConsoleModifiers.Control),
            Label = static () => "what the commands said",
            Run = static () => ViewKind.Output,
        },
        ViewCommand.For(new KeyBinding(ConsoleKey.Enter, ConsoleModifiers.Alt),
            static () => "name onto the command line", Insert),
        new()
        {
            Binding = new(ConsoleKey.Escape),
            Label = static () => "stop what is running",
            IsEnabled = () => _operations.IsBusy || _runner.IsRunning,
            Run = () =>
            {
                if (_runner.IsRunning)
                {
                    _runner.Stop();
                }
                else
                {
                    _operations.Cancel();
                }

                return ViewRoute.None;
            },
        },
    ];

    private Navigator Navigation => _services.GetRequiredService<Navigator>();

    private void Copy()
    {
        var from = Active();
        var to = Passive();
        var sources = from.Targets();

        if (Nothing(sources))
        {
            return;
        }

        _state.RequestText(
            $"Copy {Counted(sources)} to {to.Source.Label}",
            to.Folder,
            Filled,
            target => _operations.Copy(from.Source, sources, to.Source, target));
    }

    private void Move()
    {
        var from = Active();
        var to = Passive();
        var sources = from.Targets();

        if (Nothing(sources))
        {
            return;
        }

        _state.RequestText(
            $"Move {Counted(sources)} to {to.Source.Label}",
            to.Folder,
            Filled,
            target => _operations.Move(from.Source, sources, to.Source, target));
    }

    private void Rename()
    {
        var panel = Active();

        if (panel.Current is not { IsParent: false } current)
        {
            return;
        }

        _state.RequestText($"Rename {current.Name} to", current.Name, Filled, name =>
        {
            panel.State.Cursor = panel.Source.NameOf(name);
            _operations.Rename(panel.Source, current, panel.Source.Combine(panel.Folder, name));
        });
    }

    private void MakeFolder()
    {
        var panel = Active();

        _state.RequestText("Create folder", "", Filled, name => Answering(
            () => FileTasks.CreateFolderAsync(panel.Source, panel.Folder, name, CancellationToken.None),
            created =>
            {
                if (created is null)
                {
                    _state.Output = $"Could not create {name}";

                    return;
                }

                panel.State.Cursor = panel.Source.NameOf(created);
                panel.Reload();
            }));
    }

    /// <summary>
    /// Asks the source something and answers when it has answered. A key press must not wait on a
    /// server: the question goes off on its own and what comes back is handed to the drawing thread,
    /// which is the only one allowed to change what is on screen.
    /// </summary>
    /// <typeparam name="T">What was asked for.</typeparam>
    /// <param name="asking">The question.</param>
    /// <param name="answered">What to do with the answer, on the drawing thread.</param>
    private static void Answering<T>(Func<Task<T>> asking, Action<T> answered)
    {
        _ = Asked();

        async Task Asked()
        {
            var answer = await asking().ConfigureAwait(false);

            FrameThread.Post(() => answered(answer));
        }
    }

    /// <summary>
    /// Sets the permissions of what is marked. The box opens on the permissions the first of them
    /// already has, so raising one bit does not mean typing the other eight from memory.
    /// </summary>
    private void Chmod()
    {
        var panel = Active();
        var targets = panel.Targets();

        if (Nothing(targets))
        {
            return;
        }

        Answering(
            () => panel.Source.ModeAsync(targets[0], CancellationToken.None),
            current => _state.RequestText(
                $"Permissions of {Counted(targets)}",
                current.Length == 0 ? "644" : current,
                Octal,
                mode => Answering(() => Changing(panel, targets, mode), refused =>
                {
                    _state.Output = refused == 0
                        ? $"{Counted(targets)} now {mode}"
                        : $"{refused} of {targets.Count} would not take {mode}";

                    panel.Reload();
                })));
    }

    private static async Task<int> Changing(FilePanel panel, IReadOnlyList<FileEntry> targets, string mode)
    {
        var refused = 0;

        foreach (var entry in targets)
        {
            refused += await panel.Source.TryChangeModeAsync(entry, mode, CancellationToken.None)
                .ConfigureAwait(false)
                ? 0
                : 1;
        }

        return refused;
    }

    /// <summary>
    /// Hands a chown to the shell where the panel is looking. Ownership is the one thing none of the
    /// three protocols carries a request for, and a shell has said <c>chown user:group</c> for fifty
    /// years.
    /// </summary>
    private void Chown()
    {
        var panel = Active();
        var targets = panel.Targets();

        if (Nothing(targets))
        {
            return;
        }

        _state.RequestText($"Owner of {Counted(targets)}", "", Filled, owner =>
        {
            var command = new StringBuilder("chown ").Append(owner.Trim());

            foreach (var entry in targets)
            {
                command.Append(" \"").Append(entry.Name).Append('"');
            }

            _runner.Run(command.ToString(), panel.Folder, panel.Source, panel.Reload);
        });
    }

    /// <summary>
    /// Links what is under the cursor into the other panel when both are on the same source, and
    /// beside itself when they are not — a link across two machines would point at nothing.
    /// </summary>
    /// <param name="hard">Whether to make a hard link rather than a symbolic one.</param>
    private void Link(bool hard)
    {
        var panel = Active();
        var other = Passive();

        if (panel.Current is not { IsParent: false } current)
        {
            _state.Output = "Nothing to link to";
            return;
        }

        var beside = Alike(panel.Source, other.Source) ? other : panel;
        var kind = hard ? "Hard link" : "Symbolic link";

        _state.RequestText($"{kind} to {current.Name} in {beside.Folder}, named", current.Name, Filled, name =>
            Answering(
                () => beside.Source.TryLinkAsync(
                    beside.Source.Combine(beside.Folder, name.Trim()),
                    current.Path,
                    hard,
                    CancellationToken.None),
                made =>
                {
                    if (made)
                    {
                        _state.Output = $"{kind} {name.Trim()} made";
                        beside.Reload();

                        return;
                    }

                    _state.Output = $"{beside.Source.Label} would not make that {kind.ToLowerInvariant()}";
                }));
    }

    /// <summary>
    /// Whether two panels are looking at the same machine. Each panel holds a source of its own even
    /// when both are local, so this asks what the source reaches rather than which object it is.
    /// </summary>
    /// <param name="one">One panel's source.</param>
    /// <param name="other">The other's.</param>
    /// <returns><c>true</c> when a path from one means the same thing to the other.</returns>
    private static bool Alike(IFileSource one, IFileSource other) =>
        one.IsRemote == other.IsRemote && one.Label == other.Label;

    /// <summary>
    /// Marks, in both panels, everything the other panel does not have the same of — missing, a
    /// different size, or written at a different time. What is left unmarked is what matches.
    /// </summary>
    private void Compare()
    {
        _left.State.Marks.Clear();
        _right.State.Marks.Clear();

        var marked = Odd(_left, _right) + Odd(_right, _left);

        _state.Output = marked == 0 ? "The two panels hold the same files" : $"{marked} differ";
    }

    private static int Odd(FilePanel panel, FilePanel other)
    {
        var marked = 0;

        foreach (var entry in panel.Entries)
        {
            if (entry.IsParent || entry.IsFolder || Same(entry, Find(other, entry.Name)))
            {
                continue;
            }

            panel.State.Marks.Add(entry.Name);
            marked++;
        }

        return marked;
    }

    private static FileEntry? Find(FilePanel panel, string name)
    {
        foreach (var entry in panel.Entries)
        {
            if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    private static bool Same(FileEntry entry, FileEntry? other) =>
        other is not null &&
        entry.Size == other.Size &&
        Math.Abs((entry.Modified - other.Modified).TotalSeconds) < SameSecond;

    private static string? Octal(string text) =>
        Modes.Read(text) is null ? "Three octal digits, as 755" : null;

    private void Delete()
    {
        var panel = Active();
        var sources = panel.Targets();

        if (Nothing(sources))
        {
            return;
        }

        _state.RequestConfirmation(
            $"Delete {Counted(sources)}?",
            () => _operations.Delete(panel.Source, sources));
    }

    private bool Nothing(IReadOnlyList<FileEntry> sources)
    {
        if (sources.Count > 0)
        {
            return false;
        }

        _state.Output = "Nothing selected";

        return true;
    }

    private static string Counted(IReadOnlyList<FileEntry> sources) =>
        sources.Count == 1 ? sources[0].Name : $"{sources.Count} items";

    /// <summary>
    /// Checks what was typed into a dialog. A validator runs on every keystroke, so it asks only what
    /// it can answer on the spot: whether a folder is really there is a question for the source and so
    /// a wait, and one that turns out not to be is reported by the work itself rather than here.
    /// </summary>
    /// <param name="text">What was typed.</param>
    /// <returns>The complaint, or <c>null</c> when there is none.</returns>
    private static string? Filled(string text) => text.Trim().Length == 0 ? "A name is needed" : null;

    private FilePanel Active() => _right.IsFocused ? _right : _left;

    private FilePanel Passive() => _right.IsFocused ? _left : _right;

    private ViewRoute Open(FileEntry entry)
    {
        var panel = Active();

        panel.State.Cursor = entry.Name;
        _panels.Viewing.Value = entry.Path;
        _panels.ViewingSource = panel.Source;
        _panels.ViewingSize = entry.Size;

        return ViewKind.Viewer;
    }

    private ViewRoute View()
    {
        var panel = Active();

        if (panel.Current is { } current)
        {
            return panel.Activate(current);
        }

        _state.Output = "Nothing to open";

        return ViewRoute.None;
    }

    /// <summary>
    /// The menu kept in a file, the way Midnight Commander keeps one. The first time it is opened
    /// there is no file, so one is written with a few entries in it to be edited into whatever the
    /// work at hand needs.
    /// </summary>
    private void OpenUserMenu()
    {
        var entries = UserMenu.Read();

        if (entries.Count == 0)
        {
            _state.Output = UserMenu.WriteStarter()
                ? $"Wrote a menu to start from in {UserMenu.Location}"
                : $"No menu in {UserMenu.Location}";

            return;
        }

        var titles = new List<string>(entries.Count);

        foreach (var entry in entries)
        {
            titles.Add(entry.Title);
        }

        _state.RequestChoice("Menu", titles, chosen =>
        {
            foreach (var entry in entries)
            {
                if (entry.Title != chosen)
                {
                    continue;
                }

                RunEntry(entry);

                return;
            }
        });
    }

    /// <summary>
    /// Runs the commands of a menu entry, one after the other, with what the panels are pointing at
    /// put in. They are joined rather than run one at a time so that a failure stops the rest.
    /// </summary>
    /// <param name="entry">The entry that was chosen.</param>
    private void RunEntry(MenuEntry entry)
    {
        var panel = Active();
        var other = Passive();
        var marked = new StringBuilder();

        foreach (var target in panel.Targets())
        {
            marked.Append(marked.Length == 0 ? "" : " ").Append(UserMenu.Quoted(target.Name));
        }

        var whole = new StringBuilder();

        foreach (var command in entry.Commands)
        {
            whole
                .Append(whole.Length == 0 ? "" : " && ")
                .Append(UserMenu.Fill(
                    command,
                    panel.Current?.Name ?? "",
                    marked.ToString(),
                    panel.Folder,
                    other.Folder,
                    other.Current?.Name ?? ""));
        }

        _runner.Run(whole.ToString(), panel.Folder, panel.Source, panel.Reload);
    }

    /// <summary>
    /// Asks what to look for and starts the walk, which runs on its own while the results screen
    /// fills up.
    /// </summary>
    /// <returns>The results screen, or nowhere when nothing was asked for.</returns>
    private ViewRoute Find()
    {
        var panel = Active();

        _state.RequestText("Find files matching", "*", Filled, pattern =>
            _state.RequestText("Holding the text", "", null, content =>
            {
                _finder.Start(panel.Source, panel.Folder, pattern.Trim(), content.Trim(), () => { });

                Navigation.Apply(ViewKind.Find);
            }));

        return ViewRoute.None;
    }

    private void ChooseDrive(FilePanel panel) =>
        _state.RequestChoice("Drive", Listing.Drives(), panel.GoTo, panel.Folder);

    private void Filter() => Filter(Active());

    private void Filter(FilePanel panel) =>
        _state.RequestText("Show only names containing", panel.State.Filter, null, text =>
        {
            panel.State.Filter = text.Trim();
            panel.Reload();
        });

    private void OpenMenu()
    {
        var titles = new List<string>(Sections.Length);

        foreach (var section in Sections)
        {
            titles.Add(section.Title);
        }

        _state.RequestChoice("Menu", titles, OpenSection);
    }

    private void OpenSection(string title)
    {
        for (var index = 0; index < Sections.Length; index++)
        {
            if (Sections[index].Title != title)
            {
                continue;
            }

            var section = Sections[index];
            var chosen = index;

            _state.RequestChoice(section.Title, section.Items, item => Run(chosen, item));
            return;
        }
    }

    private void Run(int section, string item)
    {
        switch (section)
        {
            case 0:
                RunForPanel(_left, item);
                break;
            case 1:
                RunForFile(item);
                break;
            case 2:
                RunForBoth(item);
                break;
            case 3:
                RunForOptions(item);
                break;
            default:
                RunForPanel(_right, item);
                break;
        }
    }

    private void RunForPanel(FilePanel panel, string item)
    {
        switch (item)
        {
            case "Find file":
                Navigation.Apply(Find());
                break;
            case "Sort by name":
                panel.SortBy(Sorting.Name);
                break;
            case "Sort by size":
                panel.SortBy(Sorting.Size);
                break;
            case "Sort by date":
                panel.SortBy(Sorting.Modified);
                break;
            case "Show hidden files":
                ToggleHidden(panel);
                break;
            case "Choose drive":
                ChooseDrive(panel);
                break;
            case "Open a saved host":
                OpenSaved(panel);
                break;
            case "Connect to a server":
                Connect(panel);
                break;
            case "Disconnect":
                Disconnect(panel);
                break;
            default:
                panel.Reload();
                break;
        }
    }

    private void RunForFile(string item)
    {
        switch (item)
        {
            case "View":
                Navigation.Apply(View());
                break;
            case "Copy":
                Copy();
                break;
            case "Move":
                Move();
                break;
            case "Rename":
                Rename();
                break;
            case "Make folder":
                MakeFolder();
                break;
            case "Permissions":
                Chmod();
                break;
            case "Owner":
                Chown();
                break;
            case "Symbolic link":
                Link(hard: false);
                break;
            case "Hard link":
                Link(hard: true);
                break;
            default:
                Delete();
                break;
        }
    }

    private void RunForBoth(string item)
    {
        switch (item)
        {
            case "Swap panels":
                Swap();
                break;
            case "Both panels here":
                Passive().GoTo(Active().Folder);
                break;
            case "Compare directories":
                Compare();
                break;
            case "Folders been in":
                OpenHistory(Active());
                break;
            case "Hotlist":
                OpenHotlist();
                break;
            case "Mark a group":
                Group(marking: true);
                break;
            case "Unmark a group":
                Group(marking: false);
                break;
            case "Invert the marks":
                Active().Invert();
                break;
            case "Filter":
                Filter(Active());
                break;
            case "Run a command over SSH":
                Navigation.Apply(ViewKind.Ssh);
                break;
            case "What the commands said":
                Navigation.Apply(ViewKind.Output);
                break;
            default:
                Reload();
                break;
        }
    }

    private void RunForOptions(string item)
    {
        switch (item)
        {
            case "Hidden files here":
                ToggleHidden(_left);
                break;
            case "Hidden files there":
                ToggleHidden(_right);
                break;
            case "Notifications":
                Navigation.Apply(Routes.Notifications);
                break;
            default:
                Navigation.Apply(Routes.Help);
                break;
        }
    }

    private void OpenSaved(FilePanel panel)
    {
        var saved = SshConfig.Hosts();

        if (saved.Count == 0)
        {
            _state.Output = $"No hosts in {SshConfig.Location}";
            return;
        }

        var listed = new List<string>(saved.Count);

        foreach (var host in saved)
        {
            listed.Add(host.Describe());
        }

        _state.RequestChoice("Saved hosts", listed, chosen =>
        {
            for (var index = 0; index < listed.Count; index++)
            {
                if (listed[index] != chosen)
                {
                    continue;
                }

                Dial(panel, saved[index], saved[index].AsConnection(""));
                return;
            }
        });
    }

    private void Dial(FilePanel panel, SshHost host, Connection wanted)
    {
        _state.Output = $"Connecting to {host.Alias}…";

        Connector.Start(
            wanted,
            (source, folder) =>
            {
                _remote.Ssh = wanted;
                panel.Connect(source, folder);
                _state.Output = $"{host.Alias} · {folder}";
            },
            (message, denied) => AskPassword(panel, host, wanted, message, denied));
    }

    private void AskPassword(FilePanel panel, SshHost host, Connection wanted, string message, bool denied)
    {
        if (!denied || wanted.Password.Length > 0)
        {
            _state.RequestMessage($"Could not open {host.Alias}", message);
            return;
        }

        _state.Output = message;
        _state.RequestPassword(
            $"Password for {host.User}@{host.HostName}",
            password => Dial(panel, host, wanted with { Password = password }));
    }

    private void Connect(FilePanel panel)
    {
        _panels.RightIsActive.Value = ReferenceEquals(panel, _right);
        Navigation.Apply(ViewKind.Connect);
    }

    private void Disconnect(FilePanel panel)
    {
        if (!panel.Source.IsRemote)
        {
            _state.Output = "That panel is already local";
            return;
        }

        var label = panel.Source.Label;

        panel.Connect(new LocalSource(), Environment.CurrentDirectory);

        _state.Output = $"Disconnected from {label}";
    }

    private void ToggleHidden() => ToggleHidden(Active());

    private void ToggleHidden(FilePanel panel)
    {
        panel.State.ShowHidden = !panel.State.ShowHidden;
        panel.Reload();

        _state.Output = panel.State.ShowHidden ? "Hidden files shown" : "Hidden files skipped";
    }

    private void Swap()
    {
        var here = _left.Folder;

        _left.GoTo(_right.Folder);
        _right.GoTo(here);
    }

    /// <summary>
    /// Gives the key to the command line, which takes it only when there is something typed — an
    /// empty line leaves Space, Enter, Backspace and the marking keys to the panel.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns><c>true</c> when the line took it.</returns>
    private bool Typed(ConsoleKeyInfo key)
    {
        if (_line.IsEmpty || !_keymap.Confirm.Matches(key))
        {
            return _line.Handle(key);
        }

        Run();

        return true;
    }

    private void Prefixed(ConsoleKeyInfo key)
    {
        var panel = Active();

        switch (char.ToLowerInvariant(key.KeyChar))
        {
            case 'c':
                Chmod();
                break;
            case 'o':
                Chown();
                break;
            case 's':
                Link(hard: false);
                break;
            case 'l':
                Link(hard: true);
                break;
            case 'd':
                Compare();
                break;
            case 'p':
                _line.Insert(panel.Folder);
                break;
            case 't':
                foreach (var entry in panel.Targets())
                {
                    _line.Insert(entry.Name);
                }

                break;
            case 'h':
                Remember(panel.Folder);
                break;
            case 'j':
                Navigation.Apply(Routes.Notifications);
                break;
            default:
                _state.Output = PrefixHint;
                break;
        }
    }

    /// <summary>
    /// Runs what is on the command line where the panel is looking. A <c>cd</c> is not run at all: it
    /// moves the panel, because a shell started for one command would forget it the moment it ended.
    /// </summary>
    private void Run()
    {
        var command = _line.Take();
        var panel = Active();

        if (command.Length == 0 || Chdir(panel, command))
        {
            return;
        }

        _runner.Run(command, panel.Folder, panel.Source, panel.Reload);
    }

    private bool Chdir(FilePanel panel, string command)
    {
        if (command != "cd" && !command.StartsWith("cd ", StringComparison.Ordinal))
        {
            return false;
        }

        var wanted = command.Length > 3 ? command[3..].Trim().Trim('"') : "";

        Answering(() => Where(panel, wanted), where =>
        {
            if (where is null)
            {
                _state.Output = $"No folder {wanted}";

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

    private void Insert()
    {
        if (Active().Current is { IsParent: false } current)
        {
            _line.Insert(current.Name);
        }
    }

    private void Group(bool marking)
    {
        var panel = Active();

        _state.RequestText(marking ? "Mark files matching" : "Unmark files matching", "*", Filled, pattern =>
        {
            panel.MarkGroup(pattern.Trim(), marking);

            _state.Output = $"{panel.State.Marks.Count} marked";
        });
    }

    private void Back()
    {
        if (!Active().Back())
        {
            _state.Output = "Nothing behind this folder";
        }
    }

    private void Forward()
    {
        if (!Active().Forward())
        {
            _state.Output = "This is the newest folder";
        }
    }

    /// <summary>The other panel shows the folder under the cursor, or this one when a file is under it.</summary>
    private void Beside()
    {
        var panel = Active();
        var wanted = panel.Current is { IsFolder: true, IsParent: false } current ? current.Path : panel.Folder;

        Passive().GoTo(wanted);
    }

    private void OpenHistory(FilePanel panel)
    {
        var been = panel.State.Visited;
        var listed = new List<string>(been.Count);

        for (var index = been.Count - 1; index >= 0; index--)
        {
            if (!Has(listed, been[index]))
            {
                listed.Add(been[index]);
            }
        }

        if (listed.Count < 2)
        {
            _state.Output = "This panel has not been anywhere else";
            return;
        }

        _state.RequestChoice("Folders been in", listed, panel.GoTo, panel.Folder);
    }

    /// <summary>
    /// The kept folders, with the two entries that keep the list itself: adding where the panel is
    /// now, and dropping one that has served its purpose.
    /// </summary>
    private void OpenHotlist()
    {
        var panel = Active();
        var listed = new List<string>(_panels.Hotlist) { AddHot };

        if (_panels.Hotlist.Count > 0)
        {
            listed.Add(DropHot);
        }

        _state.RequestChoice("Hotlist", listed, chosen =>
        {
            switch (chosen)
            {
                case AddHot:
                    Remember(panel.Folder);
                    break;
                case DropHot:
                    _state.RequestChoice("Forget", new List<string>(_panels.Hotlist), Forget);
                    break;
                default:
                    panel.GoTo(chosen);
                    break;
            }
        });
    }

    private void Remember(string folder)
    {
        if (Has(_panels.Hotlist, folder))
        {
            _state.Output = "Already on the hotlist";
            return;
        }

        _panels.Hotlist.Add(folder);
        _state.Output = $"{folder} is on the hotlist";
    }

    private static bool Has(IReadOnlyList<string> folders, string folder)
    {
        foreach (var kept in folders)
        {
            if (string.Equals(kept, folder, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void Forget(string folder)
    {
        _panels.Hotlist.Remove(folder);
        _state.Output = $"{folder} is off the hotlist";
    }

    private void Reload()
    {
        _left.Reload();
        _right.Reload();

        _state.Output = "Reloaded";
    }

    /// <summary>
    /// What is running, as a card in the corner rather than a band across the screen. Work must never
    /// block the panels, and a card says so by sitting beside them: it covers a few rows of the panel
    /// it is over and nothing else, and it is gone when the work is.
    /// </summary>
    /// <param name="screen">The whole screen, which the card places itself in.</param>
    private void DrawJob(SurfaceRegion screen)
    {
        var running = _operations.IsBusy || _runner.IsRunning;
        var said = _state.Output;

        if (!running && said.Length == 0)
        {
            return;
        }

        var width = Math.Min(CardWidth, screen.Width - 4);
        var height = running && _operations.IsMeasured ? 4 : 3;

        if (width < 20 || screen.Height < height + FooterRows + 2)
        {
            return;
        }

        var card = screen
            .Rows(screen.Height - FooterRows - height, height)
            .Inset(new Margin(screen.Width - width - 2, 0, 2, 0));

        var coat = Skin.Overlay;

        card.Fill(coat.Text);
        card.Rows(0, card.Height).Inset(new Margin(0, 0, card.Width - 1, 0))
            .Fill(Skin.Paint(Skin.Crimson, running ? Skin.Crimson : Skin.AmberRule));

        var inside = card.Inset(new Margin(2, 0, 1, 0));

        if (!running)
        {
            inside.Write(0, 0, TextWidth.Truncate(said, inside.Width), coat.Warning);
            inside.Write(1, 0, "⏎ to read the rest", coat.Label);

            return;
        }

        _spinner.Advance();

        var title = _operations.IsBusy ? _operations.Progress() : _runner.Last;

        inside.Write(0, 0, TextWidth.Truncate(title, inside.Width), coat.Strong);

        if (_operations.IsMeasured)
        {
            _bar.Value = (decimal)(_operations.Share * 100);
            _bar.Draw(inside.Rows(1, 1).Inset(new Margin(0, 0, Math.Max(0, inside.Width - BarCells), 0)));
        }

        inside.Write(inside.Height - 1, 0, _spinner.Current, coat.Accent);
        inside.WriteLine(inside.Height - 1, StopsHint, coat.Label, Align.Right);
    }

    /// <summary>
    /// The line a command is typed on, prompted with where it would run. The path is shortened the way
    /// the panel above shortens it — the home folder as a tilde, and a head too long for the room cut
    /// off — so the two say the same thing about the same folder.
    /// </summary>
    /// <param name="line">The row to draw on.</param>
    private void DrawCommandLine(SurfaceRegion line)
    {
        var panel = Active();
        var where = Paths.Shortened(panel.Source, panel.Folder, PromptRoom);

        _line.Draw(line, panel.Source.IsRemote ? $"{panel.Source.Label}:{where}" : where);
    }

    /// <summary>
    /// The band along the top: what this is, which connections are open, and the two keys that lead
    /// everywhere. It is drawn on the lit surface, so the step down to the panels marks the edge
    /// between them without a rule having to be spent on it.
    /// </summary>
    /// <param name="header">The row to draw on.</param>
    private void DrawHeader(SurfaceRegion header)
    {
        var coat = Skin.Lively;

        header.Fill(coat.Text);
        header = header.Inset(new Margin(2, 0, 2, 0));

        if (header.Height < HeaderRows)
        {
            return;
        }

        var column = 0;
        header.Write(TabRow, column, "◆", coat.Accent);

        column += 2;
        header.Write(TabRow, column, "ARLECCHINO", coat.Strong);

        column += 11;
        header.Write(TabRow, column, "COMMANDER", coat.Faded);

        _tabs.Clear();

        column += 10;
        for (var index = 0; index < _panels.Sessions.Count; index++)
        {
            var session = _panels.Sessions[index];
            var label = session.Label;
            var live = index == _panels.Open.Value;

            if (column + label.Length + 6 > header.Width - 4)
            {
                break;
            }

            var under = live ? Skin.Chip : Skin.Lit;
            var lit = new Skin.Coat(under);

            header.Write(TabRow, column, new(' ', label.Length + 5), Skin.Paint(Skin.Bone, under));
            Sides(header, column + 1, session, live, lit);

            _tabs.Add((column, label.Length + 5, index));

            column += label.Length + 6;
        }

        header.WriteLine(TabRow, "Ctrl+K do anything", coat.Faded, Align.Right);
        header.Write(TabRow, column + 1, "+", coat.Trace);

        _tabs.Add((column, 3, Fresh));
    }

    /// <summary>
    /// The two sides of a tab, with the lit dot against whichever of them is being worked in. A tab
    /// holds two panels, so the dot is the only thing on it that can answer which of the two has the
    /// focus — a dot that never moves answers nothing. A side on a server is named after it, in the
    /// colour servers get, so a glance at the tab says what it is connected to.
    /// </summary>
    /// <param name="header">The band to draw on.</param>
    /// <param name="column">Where the tab's text starts.</param>
    /// <param name="session">The tab.</param>
    /// <param name="live">Whether it is the tab on screen.</param>
    /// <param name="lit">The surface of the tab.</param>
    private void Sides(SurfaceRegion header, int column, Session session, bool live, Skin.Coat lit)
    {
        var right = live && _panels.RightIsActive.Value;
        var near = Named(session.Left, live && !right, lit);
        var far = Named(session.Right, right, lit);
        var at = column;

        if (!right)
        {
            header.Write(TabRow, at, "●", live ? lit.Accent : lit.Trace);
            at += 2;
        }

        header.Write(TabRow, at, session.Near, near);
        at += session.Near.Length + 1;

        header.Write(TabRow, at, "⇄", lit.Trace);
        at += 2;

        if (right)
        {
            header.Write(TabRow, at, "●", lit.Accent);
            at += 2;
        }

        header.Write(TabRow, at, session.Far, far);
    }

    /// <summary>What colour one side of a tab is written in.</summary>
    /// <param name="state">The panel that side holds.</param>
    /// <param name="working">Whether it is the side being worked in.</param>
    /// <param name="lit">The surface of the tab.</param>
    /// <returns>The style.</returns>
    private static TermColor Named(PanelState state, bool working, Skin.Coat lit) => state.Source.IsRemote
        ? lit.Remote
        : working
            ? lit.Text
            : lit.Meta;

    /// <summary>
    /// The column between the panels. It is not decoration: it says which way the work goes and what is
    /// waiting to be worked on, written down the column a letter to a row.
    /// </summary>
    /// <param name="gutter">The column to draw on.</param>
    private void DrawGutter(SurfaceRegion gutter)
    {
        var coat = Skin.Terminal;

        gutter.Fill(coat.Text);

        if (gutter.Height < 4 || gutter.Width < 3)
        {
            return;
        }

        var marks = Active().State.Marks.Count;
        var label = marks > 0 ? $"{marks} MARKED" : "TAB";
        var style = marks > 0 ? coat.Accent : coat.Sleeping;
        var top = Math.Max(0, (gutter.Height - label.Length - 2) / 2);

        gutter.Write(top, 1, _panels.RightIsActive.Value ? "←" : "→", style);

        for (var index = 0; index < label.Length && top + index + 2 < gutter.Height; index++)
        {
            gutter.Write(top + index + 2, 1, label[index].ToString(), style);
        }
    }

    /// <summary>
    /// The bar along the bottom, which says in words what the keys of this moment do. It is not a fixed
    /// ten-slot row: what is on it follows from what is marked and where the panel is looking, and
    /// everything it leaves out is behind <c>Ctrl+K</c>.
    /// </summary>
    /// <param name="bar">The row to draw on.</param>
    private void DrawActions(SurfaceRegion bar)
    {
        var coat = Skin.Lively;

        bar.Fill(coat.Text);

        var actions = Actions();
        var column = SideRoom;

        if (Room(actions) > bar.Width - (SideRoom * 2))
        {
            actions = Actions(plain: true);
        }

        foreach (var (key, label) in actions)
        {
            var wanted = key.Length + label.Length + 4;

            if (column + wanted > bar.Width - SideRoom)
            {
                break;
            }

            bar.Write(0, column, $" {key} ", Skin.Paint(Skin.Sea, Skin.Chip));
            bar.Write(0, column + key.Length + 3, label, coat.Second);

            column += wanted;
        }
    }

    /// <summary>
    /// The ten function keys, in the order everyone who has used a file manager knows them by. What
    /// changes is not which keys are on the bar but what they are said to do: with three files marked,
    /// <c>F5</c> is no longer "copy" but "copy 3 items", and the bar has answered the question before
    /// it was asked.
    /// </summary>
    /// <param name="plain">
    /// Leaves off what is being acted on, for a terminal too narrow to say it. Ten keys that fit is
    /// worth more than eight that are spelled out and two that fell off the end.
    /// </param>
    /// <returns>The key and what pressing it would do now.</returns>
    private List<(string Key, string Label)> Actions(bool plain = false)
    {
        var panel = Active();
        var held = panel.Targets().Count;
        var many = plain || panel.State.Marks.Count == 0
            ? ""
            : held == 1 ? " 1 item" : $" {held} items";

        return
        [
            ("F1", "Help"),
            ("F2", "Menu"),
            ("F3", "View"),
            ("F4", "Filter"),
            ("F5", $"Copy{many}"),
            ("F6", $"Move{many}"),
            ("F7", plain ? "Mkdir" : "New folder"),
            ("F8", $"Delete{many}"),
            ("F9", "Commands"),
            ("F10", "Quit"),
        ];
    }

    /// <summary>How much of the bar a set of actions would take.</summary>
    /// <param name="actions">What would go on it.</param>
    /// <returns>The width they need.</returns>
    private static int Room(List<(string Key, string Label)> actions)
    {
        var wanted = 0;

        foreach (var (key, label) in actions)
        {
            wanted += key.Length + label.Length + 4;
        }

        return wanted;
    }
}
