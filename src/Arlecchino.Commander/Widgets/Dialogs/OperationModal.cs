using System;
using System.Diagnostics.CodeAnalysis;
using Arlecchino.Editing;
using Arlecchino.Input;
using Arlecchino.Modals;

namespace Arlecchino.Commander.Widgets.Dialogs;

/// <summary>
/// The operation dialog as a dialog: what it looks like is <see cref="OperationBox"/>, what it asks is
/// <see cref="Operation"/>, and this is the pair of them sitting in the one slot the framework keeps
/// for whatever is on top.
/// </summary>
public sealed class OperationModal : Modal
{
    private readonly Action<Operation>? _completing;

    private OperationSpots _spots;

    /// <summary>Opens the dialog.</summary>
    /// <param name="asking">What is being asked.</param>
    /// <param name="completing">Finishes the path in the field, when there is a path to finish.</param>
    [SetsRequiredMembers]
    public OperationModal(Operation asking, Action<Operation>? completing = null)
    {
        Asking = asking;
        Title = asking.Title;

        _completing = completing;
    }

    /// <summary>What is being asked.</summary>
    private Operation Asking { get; }

    /// <summary>
    /// The field, while the cursor is in it. With the cursor on the switches nothing here is typed into, so
    /// pasted text has nowhere to land.
    /// </summary>
    public override ITextEntry? Typing => Asking is { Chosen: < 0, FieldLabel: not null } ? Asking.Field : null;

    /// <inheritdoc/>
    public override void Draw(ModalFrame frame)
    {
        _spots = OperationBox.Draw(frame.Screen, Asking);
        Box = _spots.Box;
    }

    /// <inheritdoc/>
    public override void Handle(ModalFrame frame, KeyPress key)
    {
        if (frame.Keymap.Cancel.Matches(key))
        {
            frame.Close();

            return;
        }

        if (frame.Keymap.Confirm.Matches(key))
        {
            frame.Close();
            Asking.Confirm(Asking);

            return;
        }

        if (key.Key == ConsoleKey.Tab)
        {
            Reach(key);

            return;
        }

        if (key.Key is ConsoleKey.DownArrow or ConsoleKey.UpArrow)
        {
            Asking.Step(key.Key == ConsoleKey.DownArrow);

            return;
        }

        if (Asking.Chosen >= 0)
        {
            Switching(frame, key);

            return;
        }

        Edit(frame, key);
    }

    /// <summary>
    /// Clicks. The two buttons do what their words say on a single click, and a switch is turned by
    /// clicking its row.
    /// </summary>
    /// <param name="frame">How to close, once an answer is given.</param>
    /// <param name="mouse">The event that arrived.</param>
    public override void HandleMouse(ModalFrame frame, MouseEvent mouse)
    {
        if (mouse.Action != MouseAction.Pressed)
        {
            return;
        }

        if (_spots.Cancel.Contains(mouse.Row, mouse.Column))
        {
            frame.Close();

            return;
        }

        if (_spots.Confirm.Contains(mouse.Row, mouse.Column))
        {
            frame.Close();
            Asking.Confirm(Asking);

            return;
        }

        if (!_spots.Options.Contains(mouse.Row, mouse.Column))
        {
            return;
        }

        var (row, _) = _spots.Options.ToLocal(mouse.Row, mouse.Column);

        Asking.Chosen = row;
        Asking.Toggle();
    }

    /// <summary>
    /// Tab, which finishes a path where there is one to finish and otherwise reaches the switches. The
    /// key is the same because the intent is: get on with it without typing the rest.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    private void Reach(KeyPress key)
    {
        if (Asking is { Chosen: < 0, Over: not null } && _completing is not null)
        {
            _completing(Asking);

            return;
        }

        Asking.Step(!key.Modifiers.HasFlag(KeyModifiers.Shift));
    }

    private void Switching(ModalFrame frame, KeyPress key)
    {
        if (frame.Keys.Resolve(key) == ' ')
        {
            Asking.Toggle();
        }
    }

    private void Edit(ModalFrame frame, KeyPress key)
    {
        if (EntryKeys.Handled(Asking.Field, frame.Keymap, frame.Copy, key))
        {
            return;
        }

        if (frame.Keys.Resolve(key) is { } typed && !char.IsControl(typed))
        {
            TextEditing.Insert(Asking.Field, typed);
        }
    }
}
