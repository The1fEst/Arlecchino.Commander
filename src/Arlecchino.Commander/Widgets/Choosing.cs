using System;
using System.Collections.Generic;

namespace Arlecchino.Commander.Widgets;

/// <summary>One row of a list to choose from.</summary>
/// <param name="Label">What it is called.</param>
/// <param name="Hint">What it is, said quietly beside the name.</param>
public sealed record Pick(string Label, string Hint = "");

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
    public string Footer { get; init; } = "↑↓ pick · Enter choose · Esc close";

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
