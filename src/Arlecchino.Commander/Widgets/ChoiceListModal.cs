using System;
using System.Diagnostics.CodeAnalysis;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Modals;
using Arlecchino.Rendering;
using Arlecchino.State;

namespace Arlecchino.Commander.Widgets;

/// <summary>
/// A list to pick from, in the slot the framework keeps for whatever is on top. Typing narrows it
/// rather than jumping to a letter: a list of two hundred hosts is not one anybody wants to arrow
/// through, and narrowing is the same gesture wherever a list appears.
/// </summary>
public sealed class ChoiceListModal : CustomModal
{
    private const int PageRows = 10;

    private readonly ArlecchinoState _state;
    private readonly ArlecchinoKeymap _keymap;
    private readonly KeyText _keys;

    /// <summary>Opens the list.</summary>
    /// <param name="picking">What is being picked from.</param>
    /// <param name="state">Where the dialog lives, so that it can close itself.</param>
    /// <param name="keymap">Keys to obey.</param>
    /// <param name="keys">Turns a key press into the character it types.</param>
    [SetsRequiredMembers]
    public ChoiceListModal(Choosing picking, ArlecchinoState state, ArlecchinoKeymap keymap, KeyText keys)
    {
        ArgumentNullException.ThrowIfNull(picking);

        Picking = picking;
        Title = picking.Title;

        _state = state;
        _keymap = keymap;
        _keys = keys;
    }

    /// <summary>What is being picked from.</summary>
    public Choosing Picking { get; }

    /// <inheritdoc/>
    public override void Draw(SurfaceRegion screen) => ChoiceBox.Draw(screen, Picking);

    /// <inheritdoc/>
    public override void Handle(ConsoleKeyInfo key)
    {
        if (_keymap.Cancel.Matches(key))
        {
            _state.CloseModal();

            return;
        }

        if (_keymap.Confirm.Matches(key))
        {
            var chosen = Picking.Current;

            _state.CloseModal();

            if (chosen is not null)
            {
                Picking.Chose(chosen.Label);
            }

            return;
        }

        if (_keymap.MoveUp.Matches(key) || _keymap.MoveDown.Matches(key))
        {
            Picking.Move(_keymap.MoveDown.Matches(key) ? 1 : -1);

            return;
        }

        if (_keymap.JumpUp.Matches(key) || _keymap.JumpDown.Matches(key))
        {
            Picking.Move(_keymap.JumpDown.Matches(key) ? PageRows : -PageRows);

            return;
        }

        if (_keymap.Erase.Matches(key))
        {
            Picking.Back();

            return;
        }

        if (_keys.Resolve(key) is { } typed && !char.IsControl(typed))
        {
            Picking.Put(typed);
        }
    }
}
