using System;
using System.Collections.Generic;
using Arlecchino.Editing;

namespace Arlecchino.Commander.Widgets.Dialogs;

/// <summary>One row of a list to choose from.</summary>
/// <param name="Label">What it is called.</param>
/// <param name="Hint">What it is, said quietly beside the name.</param>
/// <param name="Key">The key that does the same thing, written at the right.</param>
/// <param name="Run">
/// What picking it does, for a list whose rows are actions rather than answers. Without this the list
/// hands the label back to whoever opened it — which is the right thing for a list of folders, and the
/// wrong thing for a list of commands where two of them may be called the same.
/// </param>
public sealed record Pick(string Label, string Hint = "", string Key = "", Action? Run = null);

/// <summary>
/// A list to pick one thing out of: a menu, the drives, the hosts, the folders been in. All of them are
/// the same question, so all of them narrow as you type.
/// </summary>
public sealed class Choosing
{
    private readonly List<Pick> _matching = [];

    private string _narrowedFor = "";

    /// <summary>What the list is called.</summary>
    public required string Title { get; init; }

    /// <summary>Everything it holds, before any narrowing.</summary>
    public required IReadOnlyList<Pick> Items { get; init; }

    /// <summary>What to do with what was picked.</summary>
    public required Action<string> OnChoice { get; init; }

    /// <summary>What is written along the bottom.</summary>
    public string Footer { get; init; } = Loc(LocString.ChoosingHints);

    /// <summary>
    /// The line the list is narrowed by: what is typed, where the caret is and what is selected in it. It is
    /// edited the way every other line the framework knows about is.
    /// </summary>
    public TextEntry Filter { get; } = new();

    /// <summary>Whatever has been typed to narrow the list.</summary>
    public string Text => Filter.Text;

    /// <summary>Which row the cursor is on, among the ones still showing.</summary>
    public int ChosenIndex { get; set; }

    /// <summary>The rows still showing, worked out again whenever what is typed has changed.</summary>
    public IReadOnlyList<Pick> Matching
    {
        get
        {
            if (_narrowedFor != Filter.Text || (_matching.Count == 0 && Filter.Text.Length == 0))
            {
                Narrow();
            }

            return _matching;
        }
    }

    /// <summary>
    /// Puts the cursor back on the first row, which is what a change to the filter comes to: the rows
    /// showing after it are a different set.
    /// </summary>
    public void Reset() => ChosenIndex = 0;

    /// <summary>The row the cursor is on, or nothing when everything was narrowed away.</summary>
    public Pick? Current => Matching.Count == 0
        ? null
        : Matching[Math.Clamp(ChosenIndex, 0, Matching.Count - 1)];

    /// <summary>Moves the cursor, stopping at either end.</summary>
    /// <param name="by">How far, and which way.</param>
    public void Move(int by) => ChosenIndex = Math.Clamp(ChosenIndex + by, 0, Math.Max(0, Matching.Count - 1));

    /// <summary>
    /// Fills the query out to as much as every remaining row agrees on, which is the shell gesture.
    /// </summary>
    public void Complete()
    {
        if (Matching.Count == 0)
        {
            return;
        }

        var stem = Matching[0].Label;

        foreach (var pick in Matching)
        {
            stem = Common(stem, pick.Label);
        }

        if (stem.Length > Filter.Text.Length)
        {
            Filter.Text = stem;
            Reset();
        }
    }

    private static string Common(string first, string second)
    {
        var stem = 0;

        while (stem < first.Length &&
               stem < second.Length &&
               char.ToLowerInvariant(first[stem]) == char.ToLowerInvariant(second[stem]))
        {
            stem++;
        }

        return first[..stem];
    }

    private void Narrow()
    {
        var text = Filter.Text;

        _matching.Clear();
        _narrowedFor = text;

        foreach (var item in Items)
        {
            if (text.Length == 0 || item.Label.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                _matching.Add(item);
            }
        }
    }
}
