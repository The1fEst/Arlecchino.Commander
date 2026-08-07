using System;
using System.Diagnostics.CodeAnalysis;
using Arlecchino.Input;
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

    private ChoiceSpots _spots;

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
    public override void Draw(ModalFrame frame)
    {
        _spots = ChoiceBox.Draw(frame.Screen, Picking);
        Box = _spots.Box;
    }

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
            if (Picking.Current is { } chosen)
            {
                Take(frame, chosen);

                return;
            }

            frame.Close();

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

    /// <summary>
    /// Clicks. A click on a row selects it, and a second click on the row already selected picks it, which is
    /// the rule everywhere else a list is clicked. A single click that runs something makes the list a place
    /// where a slip does the wrong thing rather than the next thing.
    ///
    /// A click outside the box changes nothing. A list is not dismissed by clicking away, because a
    /// stray click should not discard what has been typed into it.
    /// </summary>
    /// <param name="frame">How to close, once something is picked.</param>
    /// <param name="mouse">The event that arrived.</param>
    public override void HandleMouse(ModalFrame frame, MouseEvent mouse)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (mouse.Action is MouseAction.ScrolledUp or MouseAction.ScrolledDown && _spots.Box.Contains(mouse.Row, mouse.Column))
        {
            Picking.Move(mouse.Action == MouseAction.ScrolledDown ? 1 : -1);

            return;
        }

        if (mouse.Action != MouseAction.Pressed || !_spots.Rows.Contains(mouse.Row, mouse.Column))
        {
            return;
        }

        var (row, _) = _spots.Rows.ToLocal(mouse.Row, mouse.Column);
        var wanted = _spots.First + row;

        if (wanted >= Picking.Matching.Count)
        {
            return;
        }

        if (wanted != Picking.Chosen)
        {
            Picking.Chosen = wanted;

            return;
        }

        Take(frame, Picking.Matching[wanted]);
    }

    /// <summary>Closes the list and does what the row says, which is the same as Enter on it.</summary>
    /// <param name="frame">How to close.</param>
    /// <param name="picked">The row.</param>
    private void Take(ModalFrame frame, Pick picked)
    {
        frame.Close();

        if (picked.Run is { } run)
        {
            run();

            return;
        }

        Picking.Chose(picked.Label);
    }
}
