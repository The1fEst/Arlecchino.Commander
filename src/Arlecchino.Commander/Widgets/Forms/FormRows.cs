using System;
using System.Collections.Generic;
using Arlecchino.Focus;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Text;
using Arlecchino.Widgets;

namespace Arlecchino.Commander.Widgets.Forms;

/// <summary>
/// A column of rows to fill in, drawn the way this application draws a dialog. What a row is called is
/// written in small capitals, and what it holds sits on the chip a field wears.
/// </summary>
public sealed class FormRows : IArlecchinoInteractiveWidget
{
    private const int Between = 1;

    private readonly ArlecchinoKeymap _keymap;

    private SurfaceRegion _drawn;
    private int _first;
    private int _step = 1 + Between;

    /// <summary>Builds the column.</summary>
    /// <param name="keymap">Keys to obey, which are the ones a list is walked by.</param>
    public FormRows(ArlecchinoKeymap keymap) => _keymap = keymap;

    /// <summary>The rows, top to bottom.</summary>
    public required IReadOnlyList<FormRow> Rows { get; init; }

    /// <summary>Which row the cursor is on.</summary>
    public int Selected { get; private set; }

    /// <summary>Whether the column has the keyboard, which the framework's focus ring sets.</summary>
    public bool IsFocused { get; set; } = true;

    /// <summary>The row the cursor is on, or nothing when the form holds none.</summary>
    public FormRow? Current => Rows.Count == 0 ? null : Rows[Math.Clamp(Selected, 0, Rows.Count - 1)];

    /// <summary>
    /// Draws the rows, one blank row between them while there is room for that. A form taller than the
    /// screen closes the gaps first and scrolls after, keeping the row the cursor is on in view.
    /// </summary>
    /// <param name="region">Where to draw.</param>
    /// <returns>The rows below the last one written.</returns>
    public SurfaceRegion Draw(SurfaceRegion region)
    {
        if (Rows.Count == 0 || region.IsEmpty)
        {
            return region;
        }

        Selected = Math.Clamp(Selected, 0, Rows.Count - 1);

        _drawn = region;
        _step = Spread(region.Height) ? 1 + Between : 1;

        var showing = Math.Max(1, ((region.Height - 1) / _step) + 1);

        _first = Math.Clamp(Selected - (showing / 2), 0, Math.Max(0, Rows.Count - showing));

        var labels = 0;

        foreach (var row in Rows)
        {
            if (row.IsLabelled)
            {
                labels = Math.Max(labels, TextWidth.Of(row.Label));
            }
        }

        var at = 0;

        for (var index = _first; index < Rows.Count && at < region.Height; index++)
        {
            Rows[index].Draw(region.Rows(at, 1), labels, index == Selected);
            at += _step;
        }

        return region.Rows(Math.Min(at, region.Height), Math.Max(0, region.Height - at));
    }

    /// <summary>Whether every row fits with a blank row between them.</summary>
    /// <param name="height">The rows there are to draw in.</param>
    /// <returns><c>true</c> when the form may be spread out.</returns>
    private bool Spread(int height) => ((Rows.Count - 1) * (1 + Between)) + 1 <= height;

    /// <summary>Walks the rows, answers one, or empties it.</summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns>What was done with it.</returns>
    public FocusResult Handle(KeyPress key)
    {
        if (Rows.Count == 0)
        {
            return FocusResult.Ignored;
        }

        if (_keymap.MoveUp.Matches(key))
        {
            Selected = Math.Max(0, Selected - 1);

            return FocusResult.Handled;
        }

        if (_keymap.MoveDown.Matches(key))
        {
            Selected = Math.Min(Rows.Count - 1, Selected + 1);

            return FocusResult.Handled;
        }

        if (_keymap.Confirm.Matches(key))
        {
            Current?.Open();

            return FocusResult.Handled;
        }

        if (!_keymap.Erase.Matches(key))
        {
            return FocusResult.Ignored;
        }

        Current?.Clear();

        return FocusResult.Handled;
    }

    /// <summary>
    /// Scrolls with the wheel and answers with a click. Clicking the row the cursor is already on opens
    /// it, so pointing at a row twice reads as choose-then-answer.
    /// </summary>
    /// <param name="mouse">The event that arrived.</param>
    /// <returns>What was done with it.</returns>
    public FocusResult HandleMouse(MouseEvent mouse)
    {
        if (_drawn.IsEmpty || !_drawn.Contains(mouse.Row, mouse.Column))
        {
            return FocusResult.Ignored;
        }

        switch (mouse.Action)
        {
            case MouseAction.ScrolledUp:
                Selected = Math.Max(0, Selected - 1);

                return FocusResult.Handled;
            case MouseAction.ScrolledDown:
                Selected = Math.Min(Rows.Count - 1, Selected + 1);

                return FocusResult.Handled;
            case MouseAction.Pressed when mouse.Button == MouseButton.Left:
                return Clicked(mouse);
            default:
                return FocusResult.Ignored;
        }
    }

    /// <summary>What the rows answer to, for the box of hints the framework draws.</summary>
    /// <returns>The key and what it does, one pair each.</returns>
    public (string Key, string Description)[] Hints() =>
    [
        ($"{_keymap.MoveUp}{_keymap.MoveDown}", Loc(LocString.FormMove)),
        (_keymap.Confirm.ToString(), Loc(LocString.FormAnswer)),
        (_keymap.Erase.ToString(), Loc(LocString.FormEmpty)),
    ];

    private FocusResult Clicked(MouseEvent mouse)
    {
        var (row, _) = _drawn.ToLocal(mouse.Row, mouse.Column);
        var index = _first + (row / _step);

        if (row % _step != 0 || index >= Rows.Count)
        {
            return FocusResult.Handled;
        }

        if (index == Selected)
        {
            Rows[index].Open();
        }

        Selected = index;

        return FocusResult.Handled;
    }
}
