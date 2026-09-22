using System;
using System.IO;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Archives;
using Arlecchino.Commander.Files.Disks;
using Arlecchino.Commander.Files.Opening;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Dialogs;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Hosting;
using Arlecchino.Navigation;
using Arlecchino.State;

namespace Arlecchino.Commander.Views.Doing;

/// <summary>
///     Everything the file manager can be asked to do, in one place and under one name each. A key, a menu
///     entry and a row of the palette all name the same thing here.
/// </summary>
public sealed class Doings
{
    private readonly Finder _finder;
    private readonly Opener _opener;
    private readonly SettingBar _setting;
    private readonly ArlecchinoState _state;
    private readonly IArlecchinoTerminal _terminal;

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
    /// <param name="navigation">Where the screens of the application are reached.</param>
    /// <param name="handover">What lends the terminal to the editor while it runs.</param>
    /// <param name="settings">What is kept between runs, which the editor is named in.</param>
    /// <param name="setting">The line settings are typed on, which one menu entry opens.</param>
    /// <param name="opener">What hands a file to whatever this machine opens it with.</param>
    /// <param name="disks">What the machine has to put a panel on.</param>
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
        Navigator navigation,
        Handover handover,
        Settings settings,
        SettingBar setting,
        Opener opener,
        Disks disks)
    {
        Dialogs = dialogs;
        Panels = panels;
        Disks = disks;

        Sessions = sessions;
        Navigation = navigation;
        _state = state;
        _finder = finder;
        _terminal = terminal;
        _setting = setting;
        _opener = opener;

        Files = new(dialogs, operations, state, panels);
        Rights = new(dialogs, runner, state, panels);
        Linking = new(dialogs, state, panels);
        Dialling = new(dialogs, remote, state);
        Editing = new(handover, settings, state, panels);
    }

    /// <summary>How anything is asked.</summary>
    public Dialogs Dialogs { get; }

    /// <summary>The two panels on screen.</summary>
    public Pair Panels { get; }

    /// <summary>What the machine has to put a panel on.</summary>
    public Disks Disks { get; }

    /// <summary>Every tab and which one is open.</summary>
    public Sessions Sessions { get; }

    /// <summary>Where the screens of the application are reached.</summary>
    public Navigator Navigation { get; }

    /// <summary>Copying, moving, renaming, making a folder, deleting.</summary>
    public Deeds Files { get; }

    /// <summary>Permissions and ownership.</summary>
    public Rights Rights { get; }

    /// <summary>Symbolic and hard links.</summary>
    public Linking Linking { get; }

    /// <summary>Getting onto a server and off it.</summary>
    public Dialling Dialling { get; }

    /// <summary>Opening a file in a real editor, with the terminal lent to it while it runs.</summary>
    public Editing Editing { get; }

    /// <summary>
    ///     Asks for the settings line. It is a key of its own — the exclamation mark — and this is the same
    ///     thing for the hand that came through the menu instead.
    /// </summary>
    public void OpenSettings()
    {
        _setting.Open();
    }

    /// <summary>
    /// Reads what is under the cursor here rather than in whatever the machine opens it with. A folder
    /// is entered, since a folder has nothing to read.
    /// </summary>
    /// <returns>The reading screen, or nowhere when there is nothing under the cursor.</returns>
    public ViewRoute Read()
    {
        var panel = Panels.Active;

        if (panel.Current is not { } current)
        {
            _state.Output = Loc(LocString.SaidNothingToOpen);

            return ViewRoute.None;
        }

        return current.IsFolder ? panel.Activate(current) : Viewing(panel, current);
    }

    /// <summary>
    ///     Puts the full path of everything marked on the clipboard, one to a line, or the path under the
    ///     cursor when nothing is marked.
    /// </summary>
    public void CopyPaths() => CopyOfEach(static entry => entry.Path, LocString.SaidPathsCopied);

    /// <summary>
    ///     Puts the name of everything marked on the clipboard, extension and all, one to a line. It is the
    ///     name the file has rather than the one the column had room to draw.
    /// </summary>
    public void CopyNames() => CopyOfEach(static entry => entry.Name, LocString.SaidNamesCopied);

    /// <summary>
    ///     Puts the name of everything marked on the clipboard with the extension cut off, one to a line.
    ///     A folder has no extension to cut, so its name goes over whole.
    /// </summary>
    public void CopyBareNames() =>
        CopyOfEach(
            static entry => entry.IsFolder ? entry.Name : Path.GetFileNameWithoutExtension(entry.Name),
            LocString.SaidNamesCopied);

    /// <summary>
    ///     Puts the folder the panel is showing on the clipboard. It answers whether anything is marked,
    ///     the folder being what the panel is looking at rather than what is picked out inside it.
    /// </summary>
    public void CopyFolder()
    {
        var folder = Panels.Active.Folder;

        _terminal.CopyToClipboard(folder);

        _state.Output = Loc(LocString.SaidPathCopied, folder);
    }

    /// <summary>
    ///     Puts one line for each marked file on the clipboard, or one for the file under the cursor when
    ///     nothing is marked. It goes through the terminal, and reports how many lines were copied.
    /// </summary>
    /// <param name="read">What to take from each file.</param>
    /// <param name="manyName">What to say when more than one was copied.</param>
    private void CopyOfEach(Func<FileEntry, string> read, LocString manyName)
    {
        var panel = Panels.Active;
        var targets = panel.Targets();

        if (targets.Count == 0)
        {
            _state.Output = Loc(LocString.SaidNothingSelected);

            return;
        }

        var lines = new string[targets.Count];

        for (var at = 0; at < targets.Count; at++)
        {
            lines[at] = read(targets[at]);
        }

        _terminal.CopyToClipboard(string.Join('\n', lines));

        _state.Output = targets.Count == 1
            ? Loc(LocString.SaidPathCopied, lines[0])
            : Loc(manyName, targets.Count);
    }

    /// <summary>
    /// Hands a file to whatever this machine opens it with, which is what Enter lands on a file for. A
    /// file on a server, and a machine with no handler of any kind, are read here instead.
    /// </summary>
    /// <param name="entry">The file.</param>
    /// <returns>The reading screen, or nowhere when the file went to a window of its own.</returns>
    public ViewRoute Open(FileEntry entry)
    {
        var panel = Panels.Active;

        if (!panel.Source.IsRemote && Archives.For(entry.Path) is { } format)
        {
            EnterArchive(panel, entry, format);

            return ViewRoute.None;
        }

        if (panel.Source.IsRemote || !_opener.Works)
        {
            return Viewing(panel, entry);
        }

        panel.State.Cursor = entry.Name;
        _state.Output = Said(entry, _opener.TryOpen(entry.Path));

        return ViewRoute.None;
    }

    /// <summary>
    /// What to say about a file handed to the desktop. An archive is walked into rather than handed
    /// over, so a machine without 7-Zip says why the archive went to the desktop instead.
    /// </summary>
    /// <param name="entry">The file.</param>
    /// <param name="opened">Whether the machine had anything to open it with.</param>
    /// <returns>The words for the bar.</returns>
    private static string Said(FileEntry entry, bool opened) =>
        (opened, !SevenZip.Works && Archives.OnlySevenZip(entry.Path)) switch
        {
            (true, false) => Loc(LocString.SaidOpened, entry.Name),
            (true, true) => Loc(LocString.SaidOpenedNoArchiver, entry.Name),
            (false, false) => Loc(LocString.SaidNothingOpensIt, entry.Name),
            (false, true) => Loc(LocString.SaidNoArchiverOpensIt, entry.Name),
        };

    /// <summary>
    /// Puts a panel inside an archive, at its root. Reading what is in it is a walk of the whole file
    /// on some formats, so it is done off the drawing thread like any other reading.
    /// </summary>
    /// <param name="panel">Which panel.</param>
    /// <param name="entry">The archive.</param>
    /// <param name="format">What reads it.</param>
    private void EnterArchive(FilePanel panel, FileEntry entry, ArchiveFormat format)
    {
        Answers.From(
            () => Task.Run(() => Archives.Open(entry.Path, format)),
            source =>
            {
                if (source is null)
                {
                    _state.Output = Loc(LocString.SaidNotAnArchive, entry.Name);

                    return;
                }

                panel.State.Cursor = "";
                panel.Connect(source, RemotePaths.Root);
            });
    }

    /// <summary>
    /// Walks into the file under the cursor as an archive, whatever it is called. A file whose name
    /// says nothing of what it holds is only ever handed to the desktop by Enter.
    /// </summary>
    public void EnterAnyArchive()
    {
        var panel = Panels.Active;

        if (panel.Current is not { IsParent: false, IsFolder: false } current)
        {
            return;
        }

        if (panel.Source.IsRemote)
        {
            _state.Output = Loc(LocString.SaidArchivesHereOnly);

            return;
        }

        EnterArchive(panel, current, Archives.Fallback(current.Path));
    }

    /// <summary>
    /// Takes a panel out of an archive, back to the folder the archive sits in and onto the archive
    /// itself. A panel anywhere else has nothing above where it is, and stays there.
    /// </summary>
    public void LeaveArchive()
    {
        var panel = Panels.Active;

        if (panel.Source is not ArchiveSource archive)
        {
            return;
        }

        panel.State.Cursor = archive.Label;
        panel.Connect(new LocalSource(), archive.Folder);
    }

    /// <summary>Takes the screen to the viewer, on the file and the source it is to read.</summary>
    /// <param name="panel">Which panel the file is in.</param>
    /// <param name="entry">The file.</param>
    /// <returns>The reading screen.</returns>
    private ViewRoute Viewing(FilePanel panel, FileEntry entry)
    {
        panel.State.Cursor = entry.Name;
        Sessions.Viewing.Value = entry.Path;
        Sessions.ViewingSource = panel.Source;
        Sessions.ViewingSize = entry.Size;

        return ViewKind.Viewer;
    }

    /// <summary>
    ///     Asks for a name and starts the walk, which runs on its own while the results screen fills up.
    ///     One question and no more: the name is what a file is looked for by.
    /// </summary>
    /// <returns>Nowhere: the results screen is gone to when the question has been answered.</returns>
    public ViewRoute Find()
    {
        var panel = Panels.Active;

        Dialogs.AskFor(
            Loc(LocString.FindTitle),
            Loc(LocString.OperationName),
            "",
            Loc(LocString.FindVerb),
            name =>
            {
                _finder.Start(panel.Source, panel.Folder, name.Trim(), () => { });

                Navigation.Apply(ViewKind.Find);
            },
            Loc(LocString.FindHint));

        return ViewRoute.None;
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

                _state.Output = Loc(LocString.Marked, panel.State.Marks.Count);
            });
    }

    /// <summary>Sends a panel to another disk, chosen out of what the machine is holding.</summary>
    /// <param name="panel">Which panel.</param>
    public void ChooseDrive(FilePanel panel) => PlaceList.Open(this, panel);

    /// <summary>
    /// Sends a panel to a place, mounting it first when it is in the machine and is not mounted. The
    /// mounting is asked for rather than done here, since what answers it is a service and a password.
    /// </summary>
    /// <param name="panel">Which panel.</param>
    /// <param name="place">Where it is going.</param>
    public void ShowPlace(FilePanel panel, Place place)
    {
        if (place.IsMounted)
        {
            panel.GoTo(place.Path);

            return;
        }

        if (!Disks.Mounts)
        {
            _state.Output = Loc(LocString.SaidPlaceNotMounted, place.Label);

            return;
        }

        _state.Output = Loc(LocString.SaidMounting, place.Label);

        Answers.From(
            () => Disks.TryMountAsync(place),
            path =>
            {
                if (path.Length == 0)
                {
                    _state.Output = Loc(LocString.SaidCouldNotMount, place.Label);

                    return;
                }

                _state.Output = Loc(LocString.SaidMounted, place.Label, path);
                panel.GoTo(path);
            });
    }

    /// <summary>Shows or stops showing the hidden files of a panel.</summary>
    /// <param name="panel">Which panel.</param>
    public void ToggleHidden(FilePanel panel)
    {
        panel.State.ShowHidden = !panel.State.ShowHidden;
        panel.Reload();

        _state.Output = panel.State.ShowHidden
            ? Loc(LocString.SaidHiddenShown)
            : Loc(LocString.SaidHiddenSkipped);
    }

    /// <summary>Puts each panel where the other one was.</summary>
    public void Swap()
    {
        var folder = Panels.Left.Folder;

        Panels.Left.GoTo(Panels.Right.Folder);
        Panels.Right.GoTo(folder);
    }

    /// <summary>Marks in both panels everything the other one does not have the same of.</summary>
    public void Compare()
    {
        var marks = Difference.Mark(Panels.Left, Panels.Right);

        _state.Output = marks == 0 ? Loc(LocString.SaidSame) : Loc(LocString.SaidDiffer, marks);
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
        var folder = panel.Current is { IsFolder: true, IsParent: false } current ? current.Path : panel.Folder;

        Panels.Passive.GoTo(folder);
    }

    /// <summary>Takes the screen to where a connection is made, and remembers which panel asked.</summary>
    /// <param name="panel">The panel that would be connected.</param>
    public void Connect(FilePanel panel)
    {
        Sessions.RightIsActive.Value = ReferenceEquals(panel, Panels.Right);

        Navigation.Apply(ViewKind.Connect);
    }
}
