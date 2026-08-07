using System;
using System.Collections.Generic;
using System.Threading;
using Arlecchino.Rendering.Colors;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// The colors the redesign is drawn in: warm near-black neutrals, bone text, one crimson accent.
///
/// Every color here is paired with one of the terminal's own sixteen, so a terminal without 24-bit
/// draws the nearest thing that was chosen rather than the nearest thing arithmetic found. The
/// background steps that carry the structure are the one thing such a terminal loses — it has no
/// color for them, and a wrong one would read worse than the terminal's own background.
/// </summary>
public static class Skin
{
    /// <summary>Outside the panels: the gutter, and whatever else the screen shows through.</summary>
    public static readonly Rgb Ink = new(0x14, 0x13, 0x17);

    /// <summary>The panel being worked in, one step lighter than the one beside it.</summary>
    public static readonly Rgb Lit = new(0x17, 0x15, 0x1B);

    /// <summary>The panel that is not.</summary>
    public static readonly Rgb Unlit = new(0x13, 0x12, 0x16);

    /// <summary>Dialogs, the palette, job cards — anything drawn over the screen.</summary>
    public static readonly Rgb Over = new(0x1B, 0x18, 0x20);

    /// <summary>Key chips and inline fields, which sit on an overlay and have to be told from it.</summary>
    public static readonly Rgb Chip = new(0x24, 0x1F, 0x2A);

    /// <summary>Primary text.</summary>
    public static readonly Rgb Bone = new(0xED, 0xE6, 0xD9);

    /// <summary>The brand crimson: the focus rule, marks, carets, the primary key of the moment.</summary>
    public static readonly Rgb Crimson = new(0xC9, 0x38, 0x2B);

    /// <summary>Text and icons on a crimson fill.</summary>
    public static readonly Rgb OnCrimson = new(0xFF, 0xF3, 0xEE);

    /// <summary>The name of a file that is marked.</summary>
    public static readonly Rgb Coral = new(0xF2, 0xA0, 0x93);

    /// <summary>Host names and paths that belong to a server, and secondary key chips.</summary>
    public static readonly Rgb Sea = new(0xC9, 0xE0, 0xD9);

    /// <summary>Destroying data, and nothing else.</summary>
    public static readonly Rgb Danger = new(0xB4, 0x34, 0x2F);

    /// <summary>Something reversible and local: the rule over a rename or a new folder.</summary>
    public static readonly Rgb Calm = new(0x4E, 0x7A, 0x63);

    /// <summary>What a finished job says.</summary>
    public static readonly Rgb CalmText = new(0x9C, 0xC7, 0xAF);

    /// <summary>A locked file, a partial failure — worth reading, not worth alarm.</summary>
    public static readonly Rgb Amber = new(0xD9, 0xA0, 0x5B);

    /// <summary>The rule beside one.</summary>
    public static readonly Rgb AmberRule = new(0x8A, 0x5A, 0x2B);

    private static readonly Rgb Hairline = new(0x21, 0x1E, 0x26);
    private static readonly Rgb HairlineDim = new(0x1D, 0x1A, 0x21);
    private static readonly Rgb HairlineOver = new(0x32, 0x2C, 0x3A);
    private static readonly Rgb SecondaryInk = new(0xA7, 0x9F, 0xAE);
    private static readonly Rgb MutedInk = new(0x8A, 0x83, 0x90);
    private static readonly Rgb FaintInk = new(0x6E, 0x68, 0x70);
    private static readonly Rgb LabelInk = new(0x57, 0x51, 0x5F);
    private static readonly Rgb TraceInk = new(0x4A, 0x45, 0x50);
    private static readonly Rgb GhostInk = new(0x3A, 0x35, 0x3F);
    private static readonly Rgb IdleInk = new(0x2F, 0x2B, 0x35);

    private static readonly Dictionary<(Rgb Front, Rgb Back, TextStyle Style), TermColor> Made = [];
    private static readonly Lock Gate = new();

    /// <summary>Whatever is drawn straight on the terminal: the header, the gutter, the action bar.</summary>
    public static Coat Terminal { get; } = new(Ink);

    /// <summary>The panel being worked in.</summary>
    public static Coat Lively { get; } = new(Lit);

    /// <summary>The panel beside it.</summary>
    public static Coat Quiet { get; } = new(Unlit);

    /// <summary>A dialog, the palette, a job card.</summary>
    public static Coat Overlay { get; } = new(Over);

    /// <summary>A key chip or an inline field on an overlay.</summary>
    public static Coat Inlaid { get; } = new(Chip);

    /// <summary>
    /// This design said in the framework's own terms, so that everything the framework draws for
    /// itself — a form, a status bar, the keys screen, the file picker — comes out in it too.
    ///
    /// The panels and the dialogs of this application are painted from the coats above and answer to
    /// nothing here. Everything else is the framework's to draw, and a second screen that looks like a
    /// different program is the one thing a palette exists to prevent.
    /// </summary>
    public static ThemePalette Palette { get; } = new()
    {
        Default = Paint(Bone, Ink),
        Header = Paint(Crimson, Ink, TextStyle.Bold),
        TableHeader = Paint(LabelInk, Ink),
        Accent = Paint(Crimson, Ink),
        Info = Paint(SecondaryInk, Ink),
        Muted = Paint(MutedInk, Ink),
        Input = Paint(Bone, Chip),
        Selected = Paint(Bone, Chip),
        Active = Paint(Crimson, Ink),
        ActiveSelected = Paint(OnCrimson, Crimson, TextStyle.Bold),
        Warning = Paint(Amber, Ink),
        Error = Paint(Coral, Ink),
    };

    /// <summary>The name of the row the cursor is on: ink on bone, which no other row can be.</summary>
    public static TermColor CursorName => field ??= Paint(Ink, Bone, TextStyle.Bold);

    /// <summary>Its size and anything else that qualifies the name.</summary>
    public static TermColor CursorMeta => field ??= Paint(GhostInk, Bone);

    /// <summary>Its date, which is quieter still.</summary>
    public static TermColor CursorDate => field ??= Paint(LabelInk, Bone);

    /// <summary>Its kind tag, the one span of the row that keeps the accent.</summary>
    public static TermColor CursorTag => field ??= Paint(Crimson, Bone);

    /// <summary>The bone behind all of it, for the width the row does not write on.</summary>
    public static TermColor CursorRow => field ??= Paint(Ink, Bone);

    /// <summary>The chosen row of a list drawn over the screen, which fills with the accent instead.</summary>
    public static TermColor ChosenName => field ??= Paint(OnCrimson, Crimson, TextStyle.Bold);

    /// <summary>What qualifies it, lightened so nothing faint is left on a filled row.</summary>
    public static TermColor ChosenMeta => field ??= Paint(new(0xF0, 0xBD, 0xB5), Crimson);

    /// <summary>The accent behind all of it.</summary>
    public static TermColor ChosenRow => field ??= Paint(OnCrimson, Crimson);

    /// <summary>
    /// Crimson on crimson, which is to say no text at all: the accent as something to fill a run of
    /// cells with. It marks the edge of whatever is being worked in.
    /// </summary>
    public static TermColor CrimsonFill => field ??= Paint(Crimson, Crimson);

    /// <summary>
    /// What closes the top and bottom of the panel being worked in. It is drawn with half blocks, so the two
    /// colors are the two halves of the row: the surround above the line and the panel below it. That lets a
    /// panel end halfway down a cell instead of a whole one short.
    /// </summary>
    public static TermColor BorderActiveColor => field ??= Paint(Unlit, Lit);

    /// <summary>
    /// The same edge on the panel beside it, both halves the same, so the row is still spent on a
    /// border and nothing is drawn on it. Only one panel is worked in, and only it wears a line.
    /// </summary>
    public static TermColor BorderInactiveColor => field ??= Paint(Unlit, Unlit);

    /// <summary>
    /// A color, remembered. Styles are compared by what they are made of rather than by reference, so
    /// a row that asks for the same pairing on every frame is handed the same object and its escape
    /// sequence is built once.
    /// </summary>
    /// <param name="front">The glyphs.</param>
    /// <param name="back">What is behind them.</param>
    /// <param name="style">Bold, which is the only weight besides plain that this design uses.</param>
    /// <returns>The style.</returns>
    public static TermColor Paint(Rgb front, Rgb back, TextStyle style = TextStyle.None)
    {
        lock (Gate)
        {
            if (Made.TryGetValue((front, back, style), out var found))
            {
                return found;
            }

            var made = new TermColor
            {
                Foreground = Nearest(front),
                ExactForeground = front,
                Background = Nearest(back),
                ExactBackground = back,
                Style = style,
            };

            Made[(front, back, style)] = made;

            return made;
        }
    }

    /// <summary>
    /// What a color laid over another at part strength comes to. The design writes a marked row as
    /// crimson at 13%, which a terminal cannot do — a cell has one background and nothing behind it —
    /// so the mixture is worked out here and the result is what gets drawn.
    /// </summary>
    /// <param name="front">The color on top.</param>
    /// <param name="alpha">How much of it there is, from 0 to 1.</param>
    /// <param name="back">The color underneath.</param>
    /// <returns>The one color that looks like the two.</returns>
    public static Rgb Blend(Rgb front, double alpha, Rgb back) => new(
        Mix(front.Red, alpha, back.Red),
        Mix(front.Green, alpha, back.Green),
        Mix(front.Blue, alpha, back.Blue));

    private static byte Mix(byte front, double alpha, byte back) =>
        (byte)Math.Clamp(Math.Round((front * alpha) + (back * (1 - alpha))), 0, 255);

    /// <summary>
    /// The one of the terminal's sixteen to fall back on. Every neutral goes to bright black: they
    /// differ by a few percent of lightness, which the sixteen have no way of saying, and picking
    /// different ones for them would invent a distinction the design never asked for.
    /// </summary>
    /// <param name="colour">The exact color.</param>
    /// <returns>The nearest one that was chosen.</returns>
    private static TerminalColor Nearest(Rgb colour) => colour switch
    {
        _ when colour == Bone => TerminalColor.White,
        _ when colour == OnCrimson => TerminalColor.BrightWhite,
        _ when colour == Crimson || colour == Coral => TerminalColor.BrightRed,
        _ when colour == Danger => TerminalColor.Red,
        _ when colour == Sea => TerminalColor.Cyan,
        _ when colour == Calm || colour == CalmText => TerminalColor.Green,
        _ when colour == Amber || colour == AmberRule => TerminalColor.Yellow,
        _ when colour == SecondaryInk ||
               colour == MutedInk ||
               colour == FaintInk ||
               colour == LabelInk ||
               colour == TraceInk ||
               colour == GhostInk ||
               colour == IdleInk =>
            TerminalColor.BrightBlack,
        _ => TerminalColor.Default,
    };

    /// <summary>
    /// One surface and the text on it. The design has four backgrounds and eight weights of neutral,
    /// and a span drawn against the wrong one leaves a hole in the fill — so the surface is chosen
    /// once and every color on it comes from here.
    /// </summary>
    /// <param name="under">The background this coat is worn over.</param>
    public sealed class Coat(Rgb under)
    {
        /// <summary>Primary text: a file name, a dialog title, what was typed.</summary>
        public TermColor Text => Paint(Bone, under);

        /// <summary>The same, said louder — the folder you are in, the title of a dialog.</summary>
        public TermColor Strong => Paint(Bone, under, TextStyle.Bold);

        /// <summary>Text that is not the point but is still read.</summary>
        public TermColor Second => Paint(SecondaryInk, under);

        /// <summary>Sizes, counts, everything that qualifies a name.</summary>
        public TermColor Meta => Paint(MutedInk, under);

        /// <summary>Hints, the parent row, a plain file's tag.</summary>
        public TermColor Faded => Paint(FaintInk, under);

        /// <summary>Column heads and the small capitals that label a section.</summary>
        public TermColor Label => Paint(LabelInk, under);

        /// <summary>A date, or a count on the panel that is not being worked in.</summary>
        public TermColor Trace => Paint(TraceInk, under);

        /// <summary>Line numbers, the tag of a file worth ignoring, a hint nobody needs yet.</summary>
        public TermColor Ghost => Paint(GhostInk, under);

        /// <summary>The gutter at rest.</summary>
        public TermColor Sleeping => Paint(IdleInk, under);

        /// <summary>The accent as text: a caret, a sort arrow, the key of the moment.</summary>
        public TermColor Accent => Paint(Crimson, under);

        /// <summary>The accent as text, said louder.</summary>
        public TermColor AccentStrong => Paint(Crimson, under, TextStyle.Bold);

        /// <summary>A host name or a remote path.</summary>
        public TermColor Remote => Paint(Sea, under);

        /// <summary>A file that is locked, or a job that finished with problems.</summary>
        public TermColor Warning => Paint(Amber, under);

        /// <summary>A job that finished.</summary>
        public TermColor Done => Paint(CalmText, under);

        /// <summary>The name of a marked file, on the tinted band its row gets.</summary>
        public TermColor Marked => Paint(Coral, Tinted);

        /// <summary>Everything else in a marked row, which is tinted with it.</summary>
        public TermColor MarkedMeta => Paint(MutedInk, Tinted);

        /// <summary>The band itself, for the width the row does not write on.</summary>
        public TermColor MarkedRow => Paint(Bone, Tinted);

        /// <summary>The rule between two bands of this surface.</summary>
        public TermColor Rule => Paint(RuleInk, under);

        private Rgb Tinted => Blend(Crimson, 0.13, under);

        private Rgb RuleInk => under == Unlit
            ? HairlineDim
            : under == Over || under == Chip
                ? HairlineOver
                : Hairline;
    }
}
