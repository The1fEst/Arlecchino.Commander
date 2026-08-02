using System;
using System.Diagnostics.CodeAnalysis;
using Arlecchino.Modals;

namespace Arlecchino.Commander.Widgets.Dialogs;

/// <summary>
/// A list to pick from, in the slot the framework keeps for whatever is on top. Typing narrows it
/// rather than jumping to a letter: a list of two hundred hosts is not one anybody wants to arrow
/// through, and narrowing is the same gesture wherever a list appears.
/// </summary>
public sealed class ChoiceListModal : Modal
{
    private const int PageRows = 10;

    /// <summary>Opens the list.</summary>
    /// <param name="picking">What is being picked from.</param>
    [SetsRequiredMembers]
    public ChoiceListModal(Choosing picking)
    {
        ArgumentNullException.ThrowIfNull(picking);

        Picking = picking;
        Title = picking.Title;
    }

    /// <summary>What is being picked from.</summary>
    public Choosing Picking { get; }

    /// <inheritdoc/>
    public override void Draw(ModalFrame frame) => ChoiceBox.Draw(frame.Screen, Picking);

    /// <inheritdoc/>
    public override void Handle(ModalFrame frame, ConsoleKeyInfo key)
    {
        if (frame.Keymap.Cancel.Matches(key))
        {
            frame.Close();

            return;
        }

        if (frame.Keymap.Confirm.Matches(key))
        {
            var chosen = Picking.Current;

            frame.Close();

            if (chosen is { Run: { } run })
            {
                run();
            }
            else if (chosen is not null)
            {
                Picking.Chose(chosen.Label);
            }

            return;
        }

        if (key.Key == ConsoleKey.Tab)
        {
            Picking.Complete();

            return;
        }

        if (frame.Keymap.MoveUp.Matches(key) || frame.Keymap.MoveDown.Matches(key))
        {
            Picking.Move(frame.Keymap.MoveDown.Matches(key) ? 1 : -1);

            return;
        }

        if (frame.Keymap.JumpUp.Matches(key) || frame.Keymap.JumpDown.Matches(key))
        {
            Picking.Move(frame.Keymap.JumpDown.Matches(key) ? PageRows : -PageRows);

            return;
        }

        if (frame.Keymap.Erase.Matches(key))
        {
            Picking.Back();

            return;
        }

        if (frame.Keys.Resolve(key) is { } typed && !char.IsControl(typed))
        {
            Picking.Put(typed);
        }
    }
}
