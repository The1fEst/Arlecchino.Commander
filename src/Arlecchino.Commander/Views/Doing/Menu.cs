using System;
using System.Collections.Generic;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Commander.Widgets.Dialogs;
using Arlecchino.Navigation;

namespace Arlecchino.Commander.Views.Doing;

/// <summary>
/// The menu and the palette, which is the same menu without the filing.
///
/// Both are lists of the same entries. The menu asks which of five, then which of ten; the palette
/// asks which of ninety and narrows as you type. Neither knows what any entry does — the entry
/// carries that — so an entry cannot be listed in one and left out of the other, and neither can be
/// the place where a name and the thing it names came apart.
/// </summary>
public static class Menu
{
    /// <summary>Opens the menu at its five headings.</summary>
    /// <param name="doings">Everything the screen can do.</param>
    public static void Open(Doings doings)
    {
        ArgumentNullException.ThrowIfNull(doings);

        var sections = Sections(doings);
        var headings = new List<Pick>(sections.Count);

        foreach (var section in sections)
        {
            var which = section;

            headings.Add(new(Loc(section.Title), Run: () => Section(doings, which)));
        }

        doings.Dialogs.Pick(Loc(LocString.Menu), headings, static _ => { });
    }

    /// <summary>
    /// Everything the application can do, in one narrowing list. The bar along the bottom shows the
    /// ten keys that make sense right now; this is the other ninety — every menu entry and every key
    /// the screen knows — so nothing has to be found by remembering where it was filed.
    /// </summary>
    /// <param name="doings">Everything the screen can do.</param>
    public static void Palette(Doings doings)
    {
        ArgumentNullException.ThrowIfNull(doings);

        doings.Dialogs.Pick(
            Loc(LocString.PaletteTitle),
            Everything(doings),
            static _ => { },
            Loc(LocString.PaletteHints));
    }

    /// <summary>
    /// What is on each menu. The two panel menus are the same ten entries built twice, once against each
    /// panel. What the left menu does to the left panel the right menu does to the right, and writing that
    /// out twice is how the two would come to differ.
    /// </summary>
    /// <param name="doings">Everything the screen can do.</param>
    /// <returns>The menu.</returns>
    private static IReadOnlyList<MenuSection> Sections(Doings doings) =>
    [
        new(LocString.MenuLeft, ForPanel(doings, doings.Panels.Left)),
        new(LocString.MenuFile, ForFile(doings)),
        new(LocString.MenuCommand, ForBoth(doings)),
        new(LocString.MenuOptions, ForOptions(doings)),
        new(LocString.MenuRight, ForPanel(doings, doings.Panels.Right)),
    ];

    /// <summary>What one panel's menu holds.</summary>
    /// <param name="doings">Everything the screen can do.</param>
    /// <param name="panel">Which panel it acts on.</param>
    /// <returns>The entries.</returns>
    private static MenuItem[] ForPanel(Doings doings, FilePanel panel) =>
    [
        new(LocString.MenuFindFile, () => doings.Navigation.Apply(doings.Find())),
        new(LocString.MenuSortByName, () => panel.SortBy(Sorting.Name)),
        new(LocString.MenuSortBySize, () => panel.SortBy(Sorting.Size)),
        new(LocString.MenuSortByDate, () => panel.SortBy(Sorting.Modified)),
        new(LocString.MenuShowHidden, () => doings.ToggleHidden(panel)),
        new(LocString.MenuChooseDrive, () => doings.ChooseDrive(panel)),
        new(LocString.MenuOpenSavedHost, () => doings.Dialling.Saved(panel)),
        new(LocString.MenuConnectToServer, () => doings.Connect(panel)),
        new(LocString.MenuDisconnect, () => doings.Dialling.Disconnect(panel)),
        new(LocString.MenuReload, panel.Reload),
    ];

    /// <summary>What the file menu holds.</summary>
    /// <param name="doings">Everything the screen can do.</param>
    /// <returns>The entries.</returns>
    private static MenuItem[] ForFile(Doings doings) =>
    [
        new(LocString.View, () => doings.Navigation.Apply(doings.Read())),
        new(LocString.Copy, doings.Files.Copy),
        new(LocString.Move, doings.Files.Move),
        new(LocString.Rename, doings.Files.Rename),
        new(LocString.MenuMakeFolder, doings.Files.MakeFolder),
        new(LocString.Permissions, doings.Rights.Mode),
        new(LocString.MenuOwner, doings.Rights.Owner),
        new(LocString.MenuSymbolicLink, () => doings.Linking.Make(hard: false)),
        new(LocString.MenuHardLink, () => doings.Linking.Make(hard: true)),
        new(LocString.Delete, doings.Files.Delete),
        new(LocString.MenuDeleteForGood, doings.Files.DeleteForGood),
    ];

    /// <summary>What the menu of things done to both panels holds.</summary>
    /// <param name="doings">Everything the screen can do.</param>
    /// <returns>The entries.</returns>
    private static MenuItem[] ForBoth(Doings doings) =>
    [
        new(LocString.MenuSwapPanels, doings.Swap),
        new(LocString.MenuBothPanelsHere, () => doings.Panels.Passive.GoTo(doings.Panels.Active.Folder)),
        new(LocString.MenuCompareDirectories, doings.Compare),
        new(LocString.FoldersBeenIn, () => doings.Places.History(doings.Panels.Active)),
        new(LocString.Hotlist, () => doings.Places.Hotlist(doings.Panels.Active)),
        new(LocString.MenuCopyPaths, doings.CopyPaths),
        new(LocString.MenuMarkGroup, () => doings.Group(marking: true)),
        new(LocString.MenuUnmarkGroup, () => doings.Group(marking: false)),
        new(LocString.MenuInvertMarks, () => doings.Panels.Active.Invert()),
        new(LocString.Filter, () => doings.Filter(doings.Panels.Active)),
        new(LocString.MenuRunOverSsh, () => doings.Navigation.Apply(ViewKind.Ssh)),
        new(LocString.MenuWhatCommandsSaid, () => doings.Navigation.Apply(ViewKind.Output)),
        new(LocString.MenuReloadBoth, doings.Reload),
    ];

    /// <summary>What the menu of options holds.</summary>
    /// <param name="doings">Everything the screen can do.</param>
    /// <returns>The entries.</returns>
    private static MenuItem[] ForOptions(Doings doings) =>
    [
        new(LocString.MenuHiddenHere, () => doings.ToggleHidden(doings.Panels.Left)),
        new(LocString.MenuHiddenThere, () => doings.ToggleHidden(doings.Panels.Right)),
        new(LocString.MenuNotifications, () => doings.Navigation.Apply(Routes.Notifications)),
        new(LocString.MenuKeys, () => doings.Navigation.Apply(Routes.Help)),
    ];

    /// <summary>Opens one of the menus.</summary>
    /// <param name="doings">Everything the screen can do.</param>
    /// <param name="section">Which menu.</param>
    private static void Section(Doings doings, MenuSection section)
    {
        var items = new List<Pick>(section.Items.Count);

        foreach (var item in section.Items)
        {
            items.Add(new(Loc(item.Name), Run: item.Run));
        }

        doings.Dialogs.Pick(Loc(section.Title), items, static _ => { });
    }

    /// <summary>
    /// What goes in the palette: every menu entry, every tab, and every key the screen answers to. A
    /// menu entry says which menu it came from, so two entries called the same are told apart by where
    /// they live rather than by guessing.
    ///
    /// The tabs are here and not on a menu because a menu entry is one name known at compile time and
    /// a tab is whatever it happens to be called right now. Typing the name of a server is how you get
    /// back to the tab that is on it.
    /// </summary>
    /// <param name="doings">Everything the screen can do.</param>
    /// <returns>The rows.</returns>
    private static List<Pick> Everything(Doings doings)
    {
        var everything = new List<Pick>();

        foreach (var section in Sections(doings))
        {
            var where = Loc(section.Title).ToLowerInvariant();

            foreach (var item in section.Items)
            {
                everything.Add(new(Loc(item.Name), where, "", item.Run));
            }
        }

        everything.AddRange(TabList.Rows(doings));

        var navigation = doings.Navigation;

        foreach (var command in navigation.CurrentCommands)
        {
            if (!command.IsEnabled())
            {
                continue;
            }

            var run = command;

            everything.Add(new(
                Capitalised(command.Label()),
                Loc(LocString.PaletteKey),
                command.Binding.ToString(),
                () => navigation.Apply(run.Run())));
        }

        return everything;
    }

    /// <summary>A key's label as a row of a list rather than a word on a bar.</summary>
    /// <param name="label">What the key says it does.</param>
    /// <returns>The same, starting with a capital.</returns>
    private static string Capitalised(string label) =>
        label.Length == 0 ? label : char.ToUpperInvariant(label[0]) + label[1..];
}
