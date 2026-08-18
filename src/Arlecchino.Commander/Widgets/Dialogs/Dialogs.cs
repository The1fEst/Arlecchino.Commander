using System;
using System.Collections.Generic;
using System.Linq;
using Arlecchino.State;

namespace Arlecchino.Commander.Widgets.Dialogs;

/// <summary>
/// Every way the application has of asking something, in one place. There are two questions worth telling
/// apart, which of these and what shall it be called, and so there are two dialogs and no more.
/// </summary>
public sealed class Dialogs
{
    private readonly ArlecchinoState _state;

    /// <summary>Puts the dialogs where the framework keeps whatever is on top.</summary>
    /// <param name="state">The slot they go in.</param>
    public Dialogs(ArlecchinoState state) => _state = state;

    /// <summary>Opens a list to pick one thing out of.</summary>
    /// <param name="title">What the list is called.</param>
    /// <param name="items">What is in it.</param>
    /// <param name="onChoice">What to do with what was picked.</param>
    public void Pick(string title, IReadOnlyList<string> items, Action<string> onChoice)
    {
        Pick(title, [.. items.Select(static item => new Pick(item))], onChoice);
    }

    /// <summary>Opens a list whose rows say something about themselves as well as their name.</summary>
    /// <param name="title">What the list is called.</param>
    /// <param name="items">What is in it.</param>
    /// <param name="onChoice">What to do with what was picked.</param>
    /// <param name="footer">What is written along the bottom.</param>
    public void Pick(string title, IReadOnlyList<Pick> items, Action<string> onChoice, string? footer = null) =>
        _state.Modal = new ChoiceListModal(
            new()
            {
                Title = title,
                Items = items,
                OnChoice = onChoice,
                Footer = footer ?? Loc(LocString.ChoosingHints),
            });

    /// <summary>
    /// Opens the one dialog every operation is asked through. It goes in the slot the framework keeps for
    /// whatever is on top, so the drawing, the keys and the closing are all the framework's.
    /// </summary>
    /// <param name="operation">What to ask.</param>
    public void Ask(Operation operation) =>
        _state.Modal = new OperationModal(operation, Completion.Finish);

    /// <summary>
    /// Asks for one thing in words, through the same dialog everything else is asked through. A pattern, an
    /// owner or a filter gets the shape the large questions get.
    /// </summary>
    /// <param name="title">What the question is called.</param>
    /// <param name="label">What the field is for.</param>
    /// <param name="value">What is in it to begin with.</param>
    /// <param name="verb">The word on the button.</param>
    /// <param name="onAnswer">What to do with the answer.</param>
    /// <param name="hint">What to say beside the field.</param>
    /// <param name="secret">Whether what is typed is a secret.</param>
    public void AskFor(
        string title,
        string label,
        string value,
        string verb,
        Action<string> onAnswer,
        string hint = "",
        bool secret = false)
    {
        Ask(new()
        {
            Title = title,
            Key = "",
            Verb = verb,
            Weight = Weight.Reversible,
            FieldLabel = label,
            Value = value,
            FieldHint = hint,
            Secret = secret,
            Confirm = asking => onAnswer(asking.Value),
        });
    }

    /// <summary>
    /// Says something that needs no answer, in the same shape as everything that does. Whether it is a
    /// warning is the caller's to say, since a mark beside every message means nothing.
    /// </summary>
    /// <param name="title">What happened.</param>
    /// <param name="message">The detail of it.</param>
    /// <param name="wrong">Whether it is something that went wrong.</param>
    public void Say(string title, string message, bool wrong = true) =>
        Ask(new()
        {
            Title = title,
            Key = "",
            Verb = Loc(LocString.CloseVerb),
            Weight = wrong ? Weight.Destroys : Weight.Reversible,
            Note = _ => new(message, wrong),
            Confirm = static _ => { },
        });
}
