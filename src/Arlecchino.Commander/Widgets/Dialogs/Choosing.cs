using System;
using System.Collections.Generic;

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
/// A list to pick one thing out of: a menu, the drives, the hosts, the folders been in.
///
/// Every one of them is the same question — which of these — so they are all asked the same way, and
/// all of them narrow as you type. A menu of ten does not need narrowing; the same list of a hundred
/// hosts does, and a list that behaves differently at each size is one nobody trusts.
/// </summary>
public sealed class Choosing
{
    private readonly List<Pick> _matching = [];

    private string _typed = "";

    /// <summary>What the list is called.</summary>
    public required string Title { get; init; }

    /// <summary>Everything it holds, before any narrowing.</summary>
    public required IReadOnlyList<Pick> Items { get; init; }

    /// <summary>What to do with what was picked.</summary>
    public required Action<string> Chose { get; init; }

    /// <summary>What is written along the bottom.</summary>
    public string Footer { get; init; } = Loc(LocString.ChoosingHints);

    /// <summary>What has been typed to narrow the list.</summary>
    public string Typed
    {
        get => _typed;
        set
        {
            _typed = value;
            Chosen = 0;
            Narrow();
        }
    }

    /// <summary>Which row the cursor is on, among the ones still showing.</summary>
    public int Chosen { get; set; }

    /// <summary>The rows still showing.</summary>
    public IReadOnlyList<Pick> Matching
    {
        get
        {
            if (_matching.Count == 0 && _typed.Length == 0)
            {
                Narrow();
            }

            return _matching;
        }
    }

    /// <summary>The row the cursor is on, or nothing when everything was narrowed away.</summary>
    public Pick? Current => Matching.Count == 0
        ? null
        : Matching[Math.Clamp(Chosen, 0, Matching.Count - 1)];

    /// <summary>Moves the cursor, stopping at either end.</summary>
    /// <param name="by">How far, and which way.</param>
    public void Move(int by) => Chosen = Math.Clamp(Chosen + by, 0, Math.Max(0, Matching.Count - 1));

    /// <summary>Adds a letter to what is narrowing the list.</summary>
    /// <param name="typed">The letter.</param>
    public void Put(char typed) => Typed += typed;

    /// <summary>Takes the last letter off.</summary>
    public void Back()
    {
        if (_typed.Length > 0)
        {
            Typed = _typed[..^1];
        }
    }

    /// <summary>
    /// Fills the query out to as much as every remaining row agrees on. It is the shell gesture, and it
    /// is what turns a list of a hundred into a list of three without anybody having to read it.
    /// </summary>
    public void Complete()
    {
        if (Matching.Count == 0)
        {
            return;
        }

        var shared = Matching[0].Label;

        foreach (var pick in Matching)
        {
            shared = Common(shared, pick.Label);
        }

        if (shared.Length > _typed.Length)
        {
            Typed = shared;
        }
    }

    private static string Common(string first, string second)
    {
        var shared = 0;

        while (shared < first.Length &&
               shared < second.Length &&
               char.ToLowerInvariant(first[shared]) == char.ToLowerInvariant(second[shared]))
        {
            shared++;
        }

        return first[..shared];
    }

    private void Narrow()
    {
        _matching.Clear();

        foreach (var item in Items)
        {
            if (_typed.Length == 0 || item.Label.Contains(_typed, StringComparison.OrdinalIgnoreCase))
            {
                _matching.Add(item);
            }
        }
    }
}
