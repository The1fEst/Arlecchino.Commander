using System;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Text;

namespace Arlecchino.Commander.Widgets.Forms;

/// <summary>
/// One row of a form. There are two kinds and no more: something to fill in, and something to press once
/// the filling in is done.
/// </summary>
public abstract class FormRow
{
    /// <summary>How wide a field is drawn, however much room the screen has beyond it.</summary>
    private const int Widest = 44;

    /// <summary>What the row is called, written in small capitals at the left.</summary>
    public abstract string Label { get; }

    /// <summary>Whether the label is written in the column the labels line up in.</summary>
    public virtual bool IsLabelled => true;

    /// <summary>Answers the row: opens the dialog it is asked through, or presses the button.</summary>
    public abstract void Open();

    /// <summary>Puts the row back to nothing, which a button and a fixed value have no answer to.</summary>
    public virtual void Clear() { }

    /// <summary>Draws the row.</summary>
    /// <param name="row">The one row it is drawn on.</param>
    /// <param name="labels">How wide the column of labels came out.</param>
    /// <param name="here">Whether the cursor is on this row.</param>
    public abstract void Draw(SurfaceRegion row, int labels, bool here);

    /// <summary>How wide the field beside the labels may be.</summary>
    /// <param name="row">The row being drawn.</param>
    /// <param name="labels">How wide the column of labels came out.</param>
    /// <returns>The cells the field gets.</returns>
    protected static int Room(SurfaceRegion row, int labels) =>
        Math.Clamp(row.Width - labels - 4, 1, Widest);

    /// <summary>Writes what the row is for, after the field and only while the cursor is on it.</summary>
    /// <param name="row">The row being drawn.</param>
    /// <param name="hint">What to say.</param>
    /// <param name="at">The column the hint starts at.</param>
    protected static void Aside(SurfaceRegion row, string hint, int at)
    {
        if (hint.Length > 0 && at + 4 < row.Width)
        {
            row.Write(0, at, TextWidth.Truncate(hint, row.Width - at - 1), Skin.Terminal.Label);
        }
    }
}

/// <summary>
/// Something to fill in: a name, a port, a password, one of a list. The row shows what is held now on the
/// chip a field wears, and opening it asks for the rest through whichever dialog suits the question.
/// </summary>
/// <param name="label">What the field is called.</param>
/// <param name="value">What is in it, read afresh on every frame.</param>
/// <param name="hint">What it is for.</param>
/// <param name="open">How it is asked.</param>
/// <param name="clear">How it is emptied.</param>
public sealed class FormField(
    Func<string> label,
    Func<string> value,
    Func<string> hint,
    Action open,
    Action clear) : FormRow
{
    /// <inheritdoc/>
    public override string Label => label();

    /// <inheritdoc/>
    public override void Open() => open();

    /// <inheritdoc/>
    public override void Clear() => clear();

    /// <inheritdoc/>
    public override void Draw(SurfaceRegion row, int labels, bool here)
    {
        var coat = Skin.Terminal;
        var chip = Skin.Inlaid;
        var room = Room(row, labels);
        var run = value();

        row.Write(0, 0, here ? "❯" : " ", coat.Accent);
        row.Write(0, 2, TextWidth.Truncate(Label.ToUpperInvariant(), labels), here ? coat.Text : coat.Label);

        var field = row.Inset(new Margin(labels + 3, 0, Math.Max(0, row.Width - labels - 3 - room), 0));

        field.Fill(here ? chip.Text : chip.Meta);
        field.Write(0,
            1,
            TextWidth.Truncate(run.Length == 0 ? Loc(LocString.SettingsUnset) : run, room - 2),
            run.Length == 0 ? chip.Ghost : here ? chip.Text : chip.Second);

        if (here)
        {
            Aside(row, hint(), labels + room + 5);
        }
    }
}

/// <summary>
/// The button at the foot of a form. It wears the accent the confirming button of a dialog wears, so what
/// finishes the form looks the same wherever the form is.
/// </summary>
/// <param name="label">What the button says.</param>
/// <param name="hint">What pressing it does.</param>
/// <param name="press">What pressing it does.</param>
/// <param name="enabled">Whether the form is filled in enough for it.</param>
public sealed class FormButton(Func<string> label, Func<string> hint, Action press, Func<bool> enabled) : FormRow
{
    /// <inheritdoc/>
    public override string Label => label();

    /// <inheritdoc/>
    public override bool IsLabelled => false;

    /// <inheritdoc/>
    public override void Open()
    {
        if (enabled())
        {
            press();
        }
    }

    /// <inheritdoc/>
    public override void Draw(SurfaceRegion row, int labels, bool here)
    {
        var run = $"  {Label}  ";
        var ready = enabled();

        row.Write(0, 0, here ? "❯" : " ", Skin.Terminal.Accent);
        row.Write(0,
            2,
            run,
            ready
                ? Skin.ChosenName
                : Skin.On(Skin.Chip).Text);

        if (here)
        {
            Aside(row, hint(), run.Length + 4);
        }
    }
}
