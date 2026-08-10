using System;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Commander.Widgets.Dialogs;
using Arlecchino.Hosting;
using Arlecchino.Navigation;
using Arlecchino.State;
using Microsoft.Extensions.DependencyInjection;

namespace Arlecchino.Commander.Views.Doing;

/// <summary>
/// Everything the file manager can be asked to do, in one place and under one name each.
///
/// It is asked three ways — a key, a menu entry, a row of the palette — and all three name the same
/// thing here. That is the whole point of gathering them: a key and a menu entry that did the same
/// thing used to be two pieces of code that were only alike as long as somebody kept them alike.
/// </summary>
public sealed class Doings
{
    private readonly ArlecchinoState _state;
    private readonly Finder _finder;
    private readonly IArlecchinoTerminal _terminal;
    private readonly IServiceProvider _services;
    private readonly SettingBar _setting;

    /// <summary>Gathers everything the screen can do.</summary>
    /// <param name="dialogs">How anything is asked.</param>
    /// <param name="panels">The two panels on screen.</param>
    /// <param name="sessions">Every tab and which one is open.</param>
    /// <param name="operations">What carries file work through.</param>
    /// <param name="runner">What runs commands.</param>
    /// <param name="finder">What walks a folder looking for something.</param>
    /// <param name="remote">Where the connection that was made is remembered.</param>
    /// <param name="state">Where the last word said is kept.</param>
    /// <param name="terminal">What reaches the clipboard of the machine the user is sitting at.</param>
    /// <param name="services">Where the navigator is found, which is built after this screen is.</param>
    /// <param name="settings">What is kept between runs, which the editor is named in.</param>
    /// <param name="setting">The line settings are typed on, which one menu entry opens.</param>
    public Doings(
        Dialogs dialogs,
        Pair panels,
        Sessions sessions,
        Operations operations,
        Runner runner,
        Finder finder,
        Remote remote,
        ArlecchinoState state,
        IArlecchinoTerminal terminal,
        IServiceProvider services,
        Settings settings,
        SettingBar setting)
    {
        ArgumentNullException.ThrowIfNull(services);

        Dialogs = dialogs;
        Panels = panels;

        Sessions = sessions;
        _state = state;
        _finder = finder;
        _terminal = terminal;
        _services = services;
        _setting = setting;

        Files = new(dialogs, operations, state, panels);
        Rights = new(dialogs, runner, state, panels);
        Linking = new(dialogs, state, panels);
        Places = new(dialogs, sessions, state);
        Dialling = new(dialogs, remote, state);
        Editing = new(services.GetRequiredService<Handover>(), settings, state, panels);
    }

    /// <summary>How anything is asked.</summary>
    public Dialogs Dialogs { get; }

    /// <summary>The two panels on screen.</summary>
    public Pair Panels { get; }

    /// <summary>Every tab and which one is open.</summary>
    public Sessions Sessions { get; }

    /// <summary>Where the screens of the application are reached, resolved late because it knows them all.</summary>
    public Navigator Navigation => _services.GetRequiredService<Navigator>();

    /// <summary>Copying, moving, renaming, making a folder, deleting.</summary>
    public Deeds Files { get; }

    /// <summary>Permissions and ownership.</summary>
    public Rights Rights { get; }

    /// <summary>Symbolic and hard links.</summary>
    public Linking Linking { get; }

    /// <summary>The folders been in and the folders kept.</summary>
    public Places Places { get; }

    /// <summary>Getting onto a server and off it.</summary>
    public Dialling Dialling { get; }

    /// <summary>Opening a file in a real editor, with the terminal lent to it while it runs.</summary>
    public Editing Editing { get; }

    /// <summary>
    /// Asks for the settings line. It is a key of its own — the exclamation mark — and this is the same
    /// thing for the hand that came through the menu instead.
    /// </summary>
    public void OpenSettings() => _setting.Open();

    /// <summary>Reads what is under the cursor.</summary>
    /// <returns>The reading screen, or nowhere when there is nothing under it.</returns>
    public ViewRoute Read()
    {
        var panel = Panels.Active;

        if (panel.Current is { } current)
        {
            return panel.Activate(current);
        }

        _state.Output = Loc(LocString.SaidNothingToOpen);

        return ViewRoute.None;
    }

    /// <summary>
    /// Puts the full path of everything marked on the clipboard, one to a line, or the path under the
    /// cursor when nothing is marked.
    ///
    /// It goes through the terminal rather than through this machine, so a path copied while the panel
    /// is showing a server still lands on the clipboard of the computer the user is sitting at. A
    /// terminal with that switched off says nothing and drops it, which is why the count is reported
    /// here: it is the only sign the key did anything at all.
    /// </summary>
    public void CopyPaths()
    {
        var panel = Panels.Active;
        var targets = panel.Targets();

        if (targets.Count == 0)
        {
            _state.Output = Loc(LocString.SaidNothingSelected);

            return;
        }

        var paths = new string[targets.Count];

        for (var at = 0; at < targets.Count; at++)
        {
            paths[at] = targets[at].Path;
        }

        _terminal.CopyToClipboard(string.Join('\n', paths));

        _state.Output = targets.Count == 1
            ? Loc(LocString.SaidPathCopied, paths[0])
            : Loc(LocString.SaidPathsCopied, targets.Count);
    }

    /// <summary>Opens a file for reading, which is what a panel asks for when Enter lands on one.</summary>
    /// <param name="entry">The file.</param>
    /// <returns>The reading screen.</returns>
    public ViewRoute Open(FileEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var panel = Panels.Active;

        panel.State.Cursor = entry.Name;
        Sessions.Viewing.Value = entry.Path;
        Sessions.ViewingSource = panel.Source;
        Sessions.ViewingSize = entry.Size;

        return ViewKind.Viewer;
    }

    /// <summary>
    /// Asks what to look for and starts the walk, which runs on its own while the results screen
    /// fills up.
    /// </summary>
    /// <returns>Nowhere: the results screen is gone to when the second question has been answered.</returns>
    public ViewRoute Find()
    {
        var panel = Panels.Active;

        Dialogs.AskFor(
            Loc(LocString.FindTitle),
            Loc(LocString.OperationMatching),
            "*",
            Loc(LocString.FindNext),
            pattern => Dialogs.AskFor(
                Loc(LocString.FindTitle),
                Loc(LocString.OperationHoldingText),
                "",
                Loc(LocString.FindVerb),
                content =>
                {
                    _finder.Start(panel.Source, panel.Folder, pattern.Trim(), content.Trim(), () => { });

                    Navigation.Apply(ViewKind.Find);
                }));

        return ViewRoute.None;
    }

    /// <summary>Shows only the names holding what is typed.</summary>
    /// <param name="panel">Which panel.</param>
    public void Filter(FilePanel panel)
    {
        ArgumentNullException.ThrowIfNull(panel);

        Dialogs.AskFor(
            Loc(LocString.Filter),
            Loc(LocString.FilterField),
            panel.State.Filter,
            Loc(LocString.Filter),
            text =>
            {
                panel.State.Filter = text.Trim();
                panel.Reload();
            });
    }

    /// <summary>Marks or unmarks everything matching a pattern.</summary>
    /// <param name="marking">Whether to mark rather than unmark.</param>
    public void Group(bool marking)
    {
        var panel = Panels.Active;

        Dialogs.AskFor(
            marking ? Loc(LocString.MarkTitle) : Loc(LocString.UnmarkTitle),
            Loc(LocString.OperationMatching),
            "*",
            marking ? Loc(LocString.MarkVerb) : Loc(LocString.UnmarkVerb),
            pattern =>
            {
                panel.MarkGroup(pattern.Trim(), marking);

                _state.Output = Loc(LocString.SaidMarked, panel.State.Marks.Count);
            });
    }

    /// <summary>Sends a panel to another drive.</summary>
    /// <param name="panel">Which panel.</param>
    public void ChooseDrive(FilePanel panel)
    {
        ArgumentNullException.ThrowIfNull(panel);

        Dialogs.Pick(Loc(LocString.PickDrive), Listing.Drives(), panel.GoTo);
    }

    /// <summary>Shows or stops showing the hidden files of a panel.</summary>
    /// <param name="panel">Which panel.</param>
    public void ToggleHidden(FilePanel panel)
    {
        ArgumentNullException.ThrowIfNull(panel);

        panel.State.ShowHidden = !panel.State.ShowHidden;
        panel.Reload();

        _state.Output = panel.State.ShowHidden
            ? Loc(LocString.SaidHiddenShown)
            : Loc(LocString.SaidHiddenSkipped);
    }

    /// <summary>Puts each panel where the other one was.</summary>
    public void Swap()
    {
        var here = Panels.Left.Folder;

        Panels.Left.GoTo(Panels.Right.Folder);
        Panels.Right.GoTo(here);
    }

    /// <summary>Marks in both panels everything the other one does not have the same of.</summary>
    public void Compare()
    {
        var marked = Difference.Mark(Panels.Left, Panels.Right);

        _state.Output = marked == 0 ? Loc(LocString.SaidSame) : Loc(LocString.SaidDiffer, marked);
    }

    /// <summary>Reads both folders again.</summary>
    public void Reload()
    {
        Panels.Left.Reload();
        Panels.Right.Reload();

        _state.Output = Loc(LocString.SaidReloaded);
    }

    /// <summary>Goes back to the folder the panel was in before this one.</summary>
    public void Back()
    {
        if (!Panels.Active.Back())
        {
            _state.Output = Loc(LocString.SaidNothingBehind);
        }
    }

    /// <summary>Goes forward again, having gone back.</summary>
    public void Forward()
    {
        if (!Panels.Active.Forward())
        {
            _state.Output = Loc(LocString.SaidNewestFolder);
        }
    }

    /// <summary>The other panel shows the folder under the cursor, or this one when a file is under it.</summary>
    public void Beside()
    {
        var panel = Panels.Active;
        var wanted = panel.Current is { IsFolder: true, IsParent: false } current ? current.Path : panel.Folder;

        Panels.Passive.GoTo(wanted);
    }

    /// <summary>Takes the screen to where a connection is made, and remembers which panel asked.</summary>
    /// <param name="panel">The panel that would be connected.</param>
    public void Connect(FilePanel panel)
    {
        Sessions.RightIsActive.Value = ReferenceEquals(panel, Panels.Right);

        Navigation.Apply(ViewKind.Connect);
    }
}
