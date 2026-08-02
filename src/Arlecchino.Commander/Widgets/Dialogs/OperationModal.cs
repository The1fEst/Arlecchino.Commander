using System;
using System.Diagnostics.CodeAnalysis;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Modals;
using Arlecchino.Rendering;
using Arlecchino.State;

namespace Arlecchino.Commander.Widgets.Dialogs;

/// <summary>
/// The operation dialog as a dialog: what it looks like is <see cref="OperationBox"/>, what it asks is
/// <see cref="Operation"/>, and this is the pair of them sitting in the one slot the framework keeps
/// for whatever is on top.
/// </summary>
public sealed class OperationModal : CustomModal
{
    private readonly ArlecchinoState _state;
    private readonly ArlecchinoKeymap _keymap;
    private readonly KeyText _keys;
    private readonly Action<Operation>? _completing;

    /// <summary>Opens the dialog.</summary>
    /// <param name="asking">What is being asked.</param>
    /// <param name="state">Where the dialog lives, so that it can close itself.</param>
    /// <param name="keymap">Keys to obey.</param>
    /// <param name="keys">Turns a key press into the character it types.</param>
    /// <param name="completing">Finishes the path in the field, when there is a path to finish.</param>
    [SetsRequiredMembers]
    public OperationModal(
        Operation asking,
        ArlecchinoState state,
        ArlecchinoKeymap keymap,
        KeyText keys,
        Action<Operation>? completing = null)
    {
        ArgumentNullException.ThrowIfNull(asking);

        Asking = asking;
        Title = asking.Title;

        _state = state;
        _keymap = keymap;
        _keys = keys;
        _completing = completing;

        asking.Caret = asking.Value.Length;
    }

    /// <summary>What is being asked.</summary>
    public Operation Asking { get; }

    /// <inheritdoc/>
    public override void Draw(SurfaceRegion screen) => OperationBox.Draw(screen, Asking);

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
            _state.CloseModal();
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
            Switching(key);

            return;
        }

        Typing(key);
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

    private void Switching(ConsoleKeyInfo key)
    {
        if (_keys.Resolve(key) == ' ')
        {
            Asking.Toggle();
        }
    }

    private void Typing(ConsoleKeyInfo key)
    {
        if (_keymap.Erase.Matches(key))
        {
            Asking.Back();

            return;
        }

        if (key.Key is ConsoleKey.LeftArrow or ConsoleKey.RightArrow)
        {
            Asking.Nudge(key.Key == ConsoleKey.RightArrow ? 1 : -1);

            return;
        }

        if (_keys.Resolve(key) is { } typed && !char.IsControl(typed))
        {
            Asking.Put(typed);
        }
    }
}
