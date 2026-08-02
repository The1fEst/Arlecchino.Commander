using System;
using System.Collections.Generic;
using System.Linq;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.State;

namespace Arlecchino.Commander.Widgets.Dialogs;

/// <summary>
/// Every way the application has of asking something, in one place.
///
/// There are two questions worth telling apart — which of these, and what shall it be called — and so
/// there are two dialogs and no more. Everything that wants an answer comes through here, which is why
/// a screen never has a dialog of its own to draw, to close, or to forget to close.
/// </summary>
public sealed class Dialogs
{
    private readonly ArlecchinoState _state;
    private readonly ArlecchinoKeymap _keymap;
    private readonly KeyText _keys;

    /// <summary>Puts the dialogs where the framework keeps whatever is on top.</summary>
    /// <param name="state">The slot they go in.</param>
    /// <param name="keymap">Keys to obey.</param>
    /// <param name="keys">Turns a key press into the character it types.</param>
    public Dialogs(ArlecchinoState state, ArlecchinoKeymap keymap, KeyText keys)
    {
        _state = state;
        _keymap = keymap;
        _keys = keys;
    }

    /// <summary>Opens a list to pick one thing out of.</summary>
    /// <param name="title">What the list is called.</param>
    /// <param name="items">What is in it.</param>
    /// <param name="chose">What to do with what was picked.</param>
    public void Pick(string title, IReadOnlyList<string> items, Action<string> chose)
    {
        ArgumentNullException.ThrowIfNull(items);

        Pick(title, [.. items.Select(static item => new Pick(item))], chose);
    }

    /// <summary>Opens a list whose rows say something about themselves as well as their name.</summary>
    /// <param name="title">What the list is called.</param>
    /// <param name="items">What is in it.</param>
    /// <param name="chose">What to do with what was picked.</param>
    /// <param name="footer">What is written along the bottom.</param>
    public void Pick(string title, IReadOnlyList<Pick> items, Action<string> chose, string? footer = null) =>
        _state.Modal = new ChoiceListModal(
            new()
            {
                Title = title,
                Items = items,
                Chose = chose,
                Footer = footer ?? Loc(LocString.ChoosingHints),
            },
            _state,
            _keymap,
            _keys);

    /// <summary>
    /// Opens the one dialog every operation is asked through. It goes in the slot the framework keeps
    /// for whatever is on top, so no screen holds a dialog of its own — the drawing, the keys and the
    /// closing are all the framework's, and only what is asked is ours.
    /// </summary>
    /// <param name="operation">What to ask.</param>
    public void Ask(Operation operation) =>
        _state.Modal = new OperationModal(operation, _state, _keymap, _keys, Completion.Finish);

    /// <summary>
    /// Asks for one thing in words, through the same dialog everything else is asked through. The small
    /// questions — a pattern, an owner, a filter — get the shape the large ones get, because a second
    /// shape for small questions is a second thing to learn.
    /// </summary>
    /// <param name="title">What the question is called.</param>
    /// <param name="label">What the field is for.</param>
    /// <param name="value">What is in it to begin with.</param>
    /// <param name="verb">The word on the button.</param>
    /// <param name="answered">What to do with the answer.</param>
    /// <param name="hint">What to say beside the field.</param>
    /// <param name="secret">Whether what is typed is a secret.</param>
    public void AskFor(
        string title,
        string label,
        string value,
        string verb,
        Action<string> answered,
        string hint = "",
        bool secret = false)
    {
        ArgumentNullException.ThrowIfNull(answered);

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
            Confirm = asking => answered(asking.Value),
        });
    }

    /// <summary>
    /// Says something that needs no answer, in the same shape as everything that does. Whether it is
    /// a warning is the caller's to say: a mark beside every message teaches people to stop reading
    /// the mark, and the one time it means something is the time they will miss it.
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
