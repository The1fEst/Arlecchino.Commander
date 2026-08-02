using System;
using System.Collections.Generic;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Widgets.Panel;

/// <summary>
/// What a panel has been told to act on. A mark is kept by name rather than by row, so a folder read
/// again keeps its marks, and a file that has gone away takes its mark with it.
///
/// Folders are never marked by pattern or by inversion. Copying a folder is a walk of its own and one
/// nobody means to start by typing <c>*</c>; the one way to act on a folder is to put the cursor on it
/// and say so.
/// </summary>
public static class Marking
{
    /// <summary>Marks, or unmarks, every file whose name fits a shell pattern.</summary>
    /// <param name="state">Where the marks are kept.</param>
    /// <param name="entries">What the panel is showing.</param>
    /// <param name="pattern">The pattern, as <c>*.cs</c> or <c>a*,b*</c>.</param>
    /// <param name="marking"><c>true</c> to mark what fits, <c>false</c> to unmark it.</param>
    public static void Group(PanelState state, IReadOnlyList<FileEntry> entries, string pattern, bool marking)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(entries);

        foreach (var entry in entries)
        {
            if (entry.IsParent || entry.IsFolder || !Glob.Matches(entry.Name, pattern))
            {
                continue;
            }

            if (marking)
            {
                state.Marks.Add(entry.Name);
            }
            else
            {
                state.Marks.Remove(entry.Name);
            }
        }
    }

    /// <summary>Marks what is not marked and unmarks what is.</summary>
    /// <param name="state">Where the marks are kept.</param>
    /// <param name="entries">What the panel is showing.</param>
    public static void Invert(PanelState state, IReadOnlyList<FileEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(entries);

        foreach (var entry in entries)
        {
            if (entry.IsParent || entry.IsFolder)
            {
                continue;
            }

            if (!state.Marks.TryAdd(entry.Name))
            {
                state.Marks.Remove(entry.Name);
            }
        }
    }

    /// <summary>Marks what the cursor is on, or unmarks it when it is marked already.</summary>
    /// <param name="state">Where the marks are kept.</param>
    /// <param name="current">What the cursor is on.</param>
    /// <returns><c>true</c> when there was something to mark, and the cursor should move on.</returns>
    public static bool One(PanelState state, FileEntry? current)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (current is not { IsParent: false } entry)
        {
            return false;
        }

        if (!state.Marks.TryAdd(entry.Name))
        {
            state.Marks.Remove(entry.Name);
        }

        return true;
    }

    /// <summary>
    /// The three keys Midnight Commander gives to marking by pattern. They are read as characters
    /// rather than bound, because terminals disagree about which key a <c>+</c> came from.
    /// </summary>
    /// <param name="typed">The character that arrived.</param>
    /// <param name="state">Where the marks are kept.</param>
    /// <param name="entries">What the panel is showing.</param>
    /// <param name="asking">What opens the box a pattern is typed into.</param>
    /// <returns><c>true</c> when it was one of them.</returns>
    public static bool Typed(char typed, PanelState state, IReadOnlyList<FileEntry> entries, Action<bool>? asking)
    {
        switch (typed)
        {
            case '+':
                asking?.Invoke(true);
                return true;
            case '-':
                asking?.Invoke(false);
                return true;
            case '*':
                Invert(state, entries);
                return true;
            default:
                return false;
        }
    }
}
