using System;
using System.Collections.Generic;
using System.Threading;
using Arlecchino.Rendering.Colors;
using Arlecchino.Widgets.Text;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// The colors the redesign is drawn in: warm near-black neutrals, bone text, one crimson accent. Each is
/// paired with one of the terminal's own sixteen, for a terminal that cannot draw 24-bit color.
/// </summary>
public static class Skin
{
    public static readonly Rgb Ink = new(0x14, 0x13, 0x17);
    public static readonly Rgb LitInk = new(0x17, 0x15, 0x1B);
    public static readonly Rgb UnlitInk = new(0x13, 0x12, 0x16);
    public static readonly Rgb OverlayInk = new(0x1D, 0x1A, 0x18);
    public static readonly Rgb Chip = new(0x27, 0x23, 0x20);
    public static readonly Rgb Bone = new(0xED, 0xE6, 0xD9);
    public static readonly Rgb Crimson = new(0xC9, 0x38, 0x2B);
    public static readonly Rgb Flame = new(0xD7, 0x51, 0x47);
    public static readonly Rgb OnCrimson = new(0xFF, 0xF3, 0xEE);
    public static readonly Rgb Coral = new(0xF2, 0xA0, 0x93);
    public static readonly Rgb Sea = new(0xC9, 0xE0, 0xD9);
    public static readonly Rgb Danger = new(0xB4, 0x34, 0x2F);
    public static readonly Rgb Calm = new(0x4E, 0x7A, 0x63);
    public static readonly Rgb CalmText = new(0x9C, 0xC7, 0xAF);
    public static readonly Rgb Amber = new(0xD9, 0xA0, 0x5B);
    public static readonly Rgb AmberRule = new(0x8A, 0x5A, 0x2B);
    public static readonly Rgb Secondary = new(0xC5, 0xC3, 0xBF);
    public static readonly Rgb Stone = new(0xB3, 0xB1, 0xAB);
    public static readonly Rgb Faint = new(0xA2, 0x9F, 0x98);
    public static readonly Rgb LabelInk = new(0x94, 0x90, 0x88);
    public static readonly Rgb TraceInk = new(0x88, 0x83, 0x79);
    public static readonly Rgb GhostInk = new(0x7D, 0x78, 0x6F);
    public static readonly Rgb Idle = new(0x6C, 0x67, 0x60);
    private static readonly Rgb Hairline = new(0x2F, 0x2C, 0x28);
    private static readonly Rgb HairlineDim = new(0x27, 0x25, 0x21);
    private static readonly Rgb HairlineOverlay = new(0x3C, 0x38, 0x33);
    private static readonly Rgb OnBoneMeta = new(0x42, 0x3E, 0x38);
    private static readonly Rgb OnBoneDate = new(0x59, 0x54, 0x4C);

    private static readonly Dictionary<(Rgb Front, Rgb Back, TextStyle Style), TermColor> Cache = [];
    private static readonly Lock Gate = new();

    public static Coat Terminal { get; } = new(Ink);
    public static Coat Lively { get; } = new(LitInk);
    public static Coat Quiet { get; } = new(UnlitInk);
    public static Coat Overlay { get; } = new(OverlayInk);
    public static Coat Inlaid { get; } = new(Chip);

    public static ThemePalette Palette { get; } = new()
    {
        Default = Paint(Bone, Ink),
        Header = Paint(Flame, Ink, TextStyle.Bold),
        TableHeader = Paint(LabelInk, Ink),
        Accent = Paint(Flame, Ink),
        Info = Paint(Secondary, Ink),
        Secondary = Paint(Stone, Ink),
        Input = Paint(Bone, Chip),
        Selection = Paint(Bone, Chip),
        Active = Paint(Flame, Ink),
        ActiveSelection = Paint(OnCrimson, Crimson, TextStyle.Bold),
        Warning = Paint(Amber, Ink),
        Error = Paint(Coral, Ink),
    };

    public static TermColor CursorName => field ??= Paint(Ink, Bone, TextStyle.Bold);
    public static TermColor CursorMeta => field ??= Paint(OnBoneMeta, Bone);
    public static TermColor CursorDate => field ??= Paint(OnBoneDate, Bone);
    public static TermColor CursorTag => field ??= Paint(Crimson, Bone);
    public static TermColor CursorRow => field ??= Paint(Ink, Bone);
    public static TermColor ChosenName => field ??= Paint(OnCrimson, Crimson, TextStyle.Bold);
    public static TermColor ChosenMeta => field ??= Paint(new(0xF0, 0xBD, 0xB5), Crimson);
    public static TermColor ChosenRow => field ??= Paint(OnCrimson, Crimson);
    public static TermColor CrimsonFill => field ??= Paint(Crimson, Crimson);
    public static TermColor BorderActiveColor => field ??= Paint(UnlitInk, LitInk);
    public static TermColor BorderInactiveColor => field ??= Paint(UnlitInk, UnlitInk);

    /// <summary>
    /// How a line being typed into is written, wherever the application draws one: the selection on the
    /// sea green, and the symbol the caret stands on the other way round on the color the line belongs to.
    /// </summary>
    /// <param name="text">What the line itself is written in, which the surface under it decides.</param>
    /// <param name="caret">What is behind the symbol the caret stands on.</param>
    /// <returns>The three colors, for <see cref="EntryRow"/> and <see cref="EntryRuns"/>.</returns>
    public static EntryLook Entry(IArlecchinoColor text, Rgb caret) =>
        new(text, Paint(Ink, Sea), Paint(Ink, caret));

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
            if (Cache.TryGetValue((front, back, style), out var match))
            {
                return match;
            }

            var color = new TermColor
            {
                Foreground = Nearest(front),
                ExactForeground = front,
                Background = Nearest(back),
                ExactBackground = back,
                Style = style,
            };

            Cache[(front, back, style)] = color;

            return color;
        }
    }

    /// <summary>
    /// What a color laid over another at part strength comes to. A cell has one background and nothing
    /// behind it, so the mixture is worked out here and the result is what gets drawn.
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
    /// The one of the terminal's sixteen to fall back on. Every neutral goes to bright black, since they
    /// differ by a few percent of lightness and the sixteen have no way of saying that.
    /// </summary>
    /// <param name="colour">The exact color.</param>
    /// <returns>The nearest one that was chosen.</returns>
    private static TerminalColor Nearest(Rgb colour) => colour switch
    {
        _ when colour == Bone => TerminalColor.White,
        _ when colour == OnCrimson => TerminalColor.BrightWhite,
        _ when colour == Crimson || colour == Coral || colour == Flame => TerminalColor.BrightRed,
        _ when colour == Danger => TerminalColor.Red,
        _ when colour == Sea => TerminalColor.Cyan,
        _ when colour == Calm || colour == CalmText => TerminalColor.Green,
        _ when colour == Amber || colour == AmberRule => TerminalColor.Yellow,
        _ when colour == Secondary || colour == Stone || colour == Faint => TerminalColor.White,
        _ when colour == OnBoneMeta || colour == OnBoneDate => TerminalColor.Black,
        _ when colour == LabelInk ||
               colour == TraceInk ||
               colour == GhostInk ||
               colour == Idle =>
            TerminalColor.BrightBlack,
        _ => TerminalColor.Default,
    };

    /// <summary>
    /// One surface and the text on it. A span drawn against the wrong background leaves a hole in the
    /// fill, so the surface is chosen once and every color on it comes from here.
    /// </summary>
    /// <param name="background">The background this coat is worn over.</param>
    public sealed class Coat(Rgb background)
    {
        /// <summary>Primary text: a file name, a dialog title, what was typed.</summary>
        public TermColor Text => Paint(Bone, background);

        /// <summary>The same, said louder — the folder you are in, the title of a dialog.</summary>
        public TermColor Strong => Paint(Bone, background, TextStyle.Bold);

        /// <summary>Text that is not the point but is still read.</summary>
        public TermColor Second => Paint(Secondary, background);

        /// <summary>Sizes, counts, everything that qualifies a name.</summary>
        public TermColor Meta => Paint(Stone, background);

        /// <summary>Hints, the parent row, a plain file's tag.</summary>
        public TermColor Hint => Paint(Faint, background);

        /// <summary>Column heads and the small capitals that label a section.</summary>
        public TermColor Label => Paint(LabelInk, background);

        /// <summary>A date, or a count on the panel that is not being worked in.</summary>
        public TermColor Trace => Paint(TraceInk, background);

        /// <summary>Line numbers, the tag of a file worth ignoring, a hint not needed yet.</summary>
        public TermColor Ghost => Paint(GhostInk, background);

        /// <summary>The gutter at rest.</summary>
        public TermColor Sleeping => Paint(Idle, background);

        /// <summary>The accent as text: a caret, a sort arrow, the key of the moment.</summary>
        public TermColor Accent => Paint(Flame, background);

        /// <summary>The accent as text, said louder.</summary>
        public TermColor AccentStrong => Paint(Flame, background, TextStyle.Bold);

        /// <summary>A host name or a remote path.</summary>
        public TermColor Remote => Paint(Sea, background);

        /// <summary>A file that is locked, or a job that finished with problems.</summary>
        public TermColor Warning => Paint(Amber, background);

        /// <summary>A job that finished.</summary>
        public TermColor Success => Paint(CalmText, background);

        /// <summary>The name of a marked file, on the tinted band its row gets.</summary>
        public TermColor MarkName => Paint(Coral, MarkBand);

        /// <summary>Everything else in a marked row, which is tinted with it.</summary>
        public TermColor MarkMeta => Paint(Stone, MarkBand);

        /// <summary>The band itself, for the width the row does not write on.</summary>
        public TermColor MarkRow => Paint(Bone, MarkBand);

        /// <summary>The rule between two bands of this surface.</summary>
        public TermColor Rule => Paint(RuleInk, background);

        private Rgb MarkBand => Blend(Crimson, 0.13, background);

        private Rgb RuleInk => background == UnlitInk
            ? HairlineDim
            : background == OverlayInk || background == Chip
                ? HairlineOverlay
                : Hairline;
    }
}
