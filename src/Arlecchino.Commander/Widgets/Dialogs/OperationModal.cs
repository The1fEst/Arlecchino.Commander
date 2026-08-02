using System;
using System.Diagnostics.CodeAnalysis;
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

    /// <summary>Opens the dialog.</summary>
    /// <param name="asking">What is being asked.</param>
    /// <param name="completing">Finishes the path in the field, when there is a path to finish.</param>
    [SetsRequiredMembers]
    public OperationModal(Operation asking, Action<Operation>? completing = null)
    {
        ArgumentNullException.ThrowIfNull(asking);

        Asking = asking;
        Title = asking.Title;

        _completing = completing;

        asking.Caret = asking.Value.Length;
    }

    /// <summary>What is being asked.</summary>
    public Operation Asking { get; }

    /// <inheritdoc/>
    public override void Draw(ModalFrame frame) => OperationBox.Draw(frame.Screen, Asking);

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

        Typing(frame, key);
    }

    /// <summary>
    /// Tab, which finishes a path where there is one to finish and otherwise reaches the switches. The
    /// key is the same because the intent is: get on with it without typing the rest.
    /// </summary>
    /// <param name="key">The key that arrived.</param>
    private void Reach(ConsoleKeyInfo key)
    {
        if (Asking.Chosen < 0 && Asking.Over is not null && _completing is not null)
        {
            _completing(Asking);

            return;
        }

        Asking.Step(!key.Modifiers.HasFlag(ConsoleModifiers.Shift));
    }

    private void Switching(ModalFrame frame, ConsoleKeyInfo key)
    {
        if (frame.Keys.Resolve(key) == ' ')
        {
            Asking.Toggle();
        }
    }

    private void Typing(ModalFrame frame, ConsoleKeyInfo key)
    {
        if (frame.Keymap.Erase.Matches(key))
        {
            Asking.Back();

            return;
        }

        if (key.Key is ConsoleKey.LeftArrow or ConsoleKey.RightArrow)
        {
            Asking.Nudge(key.Key == ConsoleKey.RightArrow ? 1 : -1);

            return;
        }

        if (frame.Keys.Resolve(key) is { } typed && !char.IsControl(typed))
        {
            Asking.Put(typed);
        }
    }
}
