using System;
using System.Collections.Generic;
using System.Threading;
using Arlecchino.Rendering.Colors;
using Arlecchino.Widgets.Text;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// The colors the redesign is drawn in, worked out against whatever the terminal turned out to be. What is
/// written down is how far apart things should read; the colors themselves are arrived at by
/// <see cref="Wear"/>.
/// </summary>
public static class Skin
{
    /// <summary>The near-black this design was drawn on, kept for a terminal that will not say.</summary>
    private static readonly Rgb DrawnInk = new(0x14, 0x13, 0x17);

    private static readonly Dictionary<(Rgb Front, Rgb? Back, TextStyle Style), TermColor> Cache = [];
    private static readonly Dictionary<Rgb, TerminalColor> Sixteen = [];
    private static readonly Dictionary<(Rgb Surface, Tone Tone), Rgb> Answers = [];
    private static readonly Dictionary<Rgb, Coat> Coats = [];
    private static readonly Lock Gate = new();

    static Skin() => Wear(DrawnInk);

    /// <summary>The terminal's own background, which the base surface leaves alone rather than paints.</summary>
    public static Rgb Ink { get; private set; }

    /// <summary>How far the accent wheel has turned to sit well on the terminal's own color.</summary>
    private static double AccentTurn { get; set; }

    public static Rgb LitInk { get; private set; }
    public static Rgb UnlitInk { get; private set; }
    public static Rgb OverlayInk { get; private set; }
    public static Rgb Chip { get; private set; }
    public static Rgb Bone { get; private set; }
    public static Rgb Crimson { get; private set; }
    public static Rgb Flame { get; private set; }
    public static Rgb OnCrimson { get; private set; }
    public static Rgb Coral { get; private set; }
    public static Rgb Sea { get; private set; }
    public static Rgb Danger { get; private set; }
    public static Rgb Calm { get; private set; }
    public static Rgb CalmText { get; private set; }
    public static Rgb Amber { get; private set; }
    public static Rgb AmberRule { get; private set; }
    public static Rgb Secondary { get; private set; }
    public static Rgb Stone { get; private set; }
    public static Rgb Faint { get; private set; }
    public static Rgb LabelInk { get; private set; }
    public static Rgb TraceInk { get; private set; }
    public static Rgb GhostInk { get; private set; }
    public static Rgb Idle { get; private set; }

    private static Rgb Hairline { get; set; }
    private static Rgb HairlineDim { get; set; }
    private static Rgb HairlineOverlay { get; set; }
    private static Rgb OnBoneMeta { get; set; }
    private static Rgb OnBoneDate { get; set; }
    private static Rgb OnBoneName { get; set; }

    /// <summary>The base surface, which is the terminal's own and is therefore never filled in.</summary>
    public static Coat Terminal { get; private set; } = null!;

    public static Coat Lively { get; private set; } = null!;
    public static Coat Quiet { get; private set; } = null!;
    public static Coat Overlay { get; private set; } = null!;
    public static Coat Inlaid { get; private set; } = null!;

    public static ThemePalette Palette { get; private set; } = null!;

    public static TermColor CursorName { get; private set; } = null!;
    public static TermColor CursorMeta { get; private set; } = null!;
    public static TermColor CursorDate { get; private set; } = null!;
    public static TermColor CursorTag { get; private set; } = null!;
    public static TermColor CursorRow { get; private set; } = null!;
    public static TermColor ChosenName { get; private set; } = null!;
    public static TermColor ChosenMeta { get; private set; } = null!;
    public static TermColor ChosenRow { get; private set; } = null!;
    public static TermColor CrimsonFill { get; private set; } = null!;
    public static TermColor BorderActiveColor { get; private set; } = null!;
    public static TermColor BorderInactiveColor { get; private set; } = null!;

    /// <summary>
    /// Works every color out against one background and puts them in place. It is called as the
    /// application starts, once the terminal has said what color it is.
    /// </summary>
    /// <param name="background">What the terminal draws behind the text.</param>
    public static void Wear(Rgb background)
    {
        lock (Gate)
        {
            Cache.Clear();
            Sixteen.Clear();
            Answers.Clear();
            Coats.Clear();
        }

        Ink = background;
        AccentTurn = Shade.Turn(background, AccentTone.Hue, Harmony);

        UnlitInk = Shade.Lifted(background, -0.005d);
        LitInk = Shade.Lifted(background, 0.011d);
        OverlayInk = Shade.Lifted(background, 0.031d);
        Chip = Shade.Lifted(background, 0.070d);
        HairlineDim = Shade.Lifted(background, 0.075d);
        Hairline = Shade.Lifted(background, 0.105d);
        HairlineOverlay = Shade.Lifted(background, 0.153d);

        Bone = Ladder(background, TextTone);
        Secondary = Ladder(background, SideText);
        Stone = Ladder(background, Qualifier);
        Faint = Ladder(background, HintTone);
        LabelInk = Ladder(background, LabelTone);
        TraceInk = Ladder(background, TraceTone);
        GhostInk = Ladder(background, GhostTone);
        Idle = Ladder(background, Gutter);

        Crimson = Ladder(background, AccentTone);
        Flame = Ladder(background, AccentLoud);
        Coral = Ladder(background, AccentSoft);
        Sea = Ladder(background, Distance);
        Calm = Ladder(background, Peace);
        CalmText = Ladder(background, PeaceText);
        Amber = Ladder(background, Caution);
        AmberRule = Ladder(background, CautionRule);
        Danger = Ladder(background, Alarm);

        OnCrimson = Ladder(Crimson, OnAccent, OnAccentSoft.Contrast, OnAccent.Contrast);
        OnBoneName = Ladder(Bone, TextTone, AccentOnLight.Contrast, TextTone.Contrast);
        OnBoneMeta = Ladder(Bone, OnLightMeta, AccentOnLight.Contrast, TextTone.Contrast);
        OnBoneDate = Ladder(Bone, OnLightDate, AccentOnLight.Contrast, TextTone.Contrast);

        Named();

        Terminal = new(background, own: true);
        Lively = new(LitInk, own: false);
        Quiet = new(UnlitInk, own: false);
        Overlay = new(OverlayInk, own: false);
        Inlaid = new(Chip, own: false);

        CursorName = Paint(OnBoneName, Bone, TextStyle.Bold);
        CursorMeta = Paint(OnBoneMeta, Bone);
        CursorDate = Paint(OnBoneDate, Bone);
        CursorTag = Paint(Ladder(Bone, AccentOnLight, AccentOnLight.Contrast, TextTone.Contrast), Bone);
        CursorRow = Paint(OnBoneName, Bone);
        ChosenName = Paint(OnCrimson, Crimson, TextStyle.Bold);
        ChosenMeta = Paint(Ladder(Crimson, OnAccentSoft, OnAccentSoft.Contrast, OnAccent.Contrast), Crimson);
        ChosenRow = Paint(OnCrimson, Crimson);
        CrimsonFill = Paint(Crimson, Crimson);
        BorderActiveColor = Paint(UnlitInk, LitInk);
        BorderInactiveColor = Paint(UnlitInk, UnlitInk);

        Palette = new()
        {
            Default = Paint(Bone, null),
            Header = Paint(Flame, null, TextStyle.Bold),
            TableHeader = Paint(LabelInk, null),
            Accent = Paint(Flame, null),
            Info = Paint(Secondary, null),
            Secondary = Paint(Stone, null),
            Input = Paint(Ladder(Chip, TextTone), Chip),
            Selection = Paint(Ladder(Chip, TextTone), Chip),
            Active = Paint(Flame, null),
            ActiveSelection = Paint(OnCrimson, Crimson, TextStyle.Bold),
            Warning = Paint(Amber, null),
            Error = Paint(Coral, null),
        };
    }

    /// <summary>
    /// The coat for any surface the application paints, which is how a widget asks for a color without
    /// knowing what the terminal turned out to be. One coat is kept per surface and reused.
    /// </summary>
    /// <param name="surface">What is behind the text.</param>
    /// <returns>The colors that read on it.</returns>
    public static Coat On(Rgb surface)
    {
        lock (Gate)
        {
            if (Coats.TryGetValue(surface, out var found))
            {
                return found;
            }

            var coat = new Coat(surface);

            Coats[surface] = coat;

            return coat;
        }
    }

    /// <summary>
    /// How a line being typed into is written, wherever the application draws one: the selection on the
    /// sea green, and the symbol the caret stands on the other way round on the color the line belongs to.
    /// </summary>
    /// <param name="text">What the line itself is written in, which the surface under it decides.</param>
    /// <param name="caret">What is behind the symbol the caret stands on.</param>
    /// <returns>The three colors, for <see cref="EntryRow"/> and <see cref="EntryRuns"/>.</returns>
    public static EntryLook Entry(IArlecchinoColor text, Rgb caret) =>
        new(text, Paint(Ladder(Sea, TextTone), Sea), Paint(Ladder(caret, TextTone), caret));

    /// <summary>
    /// A color, remembered. Styles are compared by what they are made of rather than by reference, so
    /// a row that asks for the same pairing on every frame is handed the same object and its escape
    /// sequence is built once.
    /// </summary>
    /// <param name="front">The glyphs.</param>
    /// <param name="back">What is behind them, or nothing to leave the terminal's own showing.</param>
    /// <param name="style">Bold, which is the only weight besides plain that this design uses.</param>
    /// <returns>The style.</returns>
    public static TermColor Paint(Rgb front, Rgb? back, TextStyle style = TextStyle.None)
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
                Background = back is null ? TerminalColor.Default : Nearest(back.Value),
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
    /// One color of the design, said as what it is for rather than as what it comes to. It keeps a hue,
    /// reads a set distance from its background, and names one of the sixteen to stand in for it.
    /// </summary>
    /// <param name="Hue">Degrees around the wheel.</param>
    /// <param name="Chroma">How far from gray.</param>
    /// <param name="Contrast">How far from the surface it is read on.</param>
    /// <param name="Ansi">The one of the sixteen to fall back on.</param>
    /// <param name="Turns">Whether it belongs to the accent wheel, which turns with the background.</param>
    private readonly record struct Tone(
        double Hue,
        double Chroma,
        double Contrast,
        TerminalColor Ansi,
        bool Turns = false);

    /// <summary>How far a marked row's band stands off the surface it is drawn on, in lightness.</summary>
    private const double BandStep = 0.055d;

    /// <summary>The least chroma a marked row's band carries, so it reads as tinted and not as gray.</summary>
    private const double BandTint = 0.045d;

    /// <summary>How far from the background's own hue the accent is put, where the background has one.</summary>
    private const double Harmony = 40d;

    private static readonly Tone TextTone = new(83.1d, 0.019d, 14.91d, TerminalColor.White);
    private static readonly Tone SideText = new(84.6d, 0.006d, 10.51d, TerminalColor.White);
    private static readonly Tone Qualifier = new(91.5d, 0.009d, 8.63d, TerminalColor.White);
    private static readonly Tone HintTone = new(87.5d, 0.011d, 7.00d, TerminalColor.White);
    private static readonly Tone LabelTone = new(84.6d, 0.013d, 5.82d, TerminalColor.BrightBlack);
    private static readonly Tone TraceTone = new(84.6d, 0.016d, 4.91d, TerminalColor.BrightBlack);
    private static readonly Tone GhostTone = new(82.4d, 0.015d, 4.22d, TerminalColor.BrightBlack);
    private static readonly Tone Gutter = new(76.5d, 0.013d, 3.30d, TerminalColor.BrightBlack);

    private static readonly Tone AccentTone = new(29.3d, 0.184d, 3.60d, TerminalColor.BrightRed, Turns: true);
    private static readonly Tone AccentLoud = new(27.5d, 0.171d, 4.54d, TerminalColor.BrightRed, Turns: true);
    private static readonly Tone AccentSoft = new(29.4d, 0.100d, 9.01d, TerminalColor.BrightRed, Turns: true);
    private static readonly Tone Distance = new(175.5d, 0.026d, 13.34d, TerminalColor.Cyan, Turns: true);
    private static readonly Tone Peace = new(160.6d, 0.061d, 3.78d, TerminalColor.Green, Turns: true);
    private static readonly Tone PeaceText = new(160.3d, 0.057d, 9.87d, TerminalColor.Green, Turns: true);
    private static readonly Tone Caution = new(70.0d, 0.110d, 8.04d, TerminalColor.Yellow, Turns: true);
    private static readonly Tone CautionRule = new(62.8d, 0.089d, 3.15d, TerminalColor.Yellow, Turns: true);
    private static readonly Tone Alarm = new(26.9d, 0.165d, 3.06d, TerminalColor.Red, Turns: true);

    private static readonly Tone OnAccent = new(44.2d, 0.015d, 4.73d, TerminalColor.BrightWhite, Turns: true);
    private static readonly Tone OnAccentSoft = new(28.7d, 0.060d, 3.10d, TerminalColor.BrightWhite, Turns: true);
    private static readonly Tone AccentOnLight = new(29.3d, 0.184d, 4.14d, TerminalColor.BrightRed, Turns: true);
    private static readonly Tone OnLightMeta = new(78.2d, 0.011d, 8.56d, TerminalColor.Black);
    private static readonly Tone OnLightDate = new(79.7d, 0.014d, 6.05d, TerminalColor.Black);


    /// <summary>
    /// One color of the design worked out against the surface it is read on. On the terminal's own
    /// background the whole ladder is brought down to the room there is, so its steps stay apart.
    /// </summary>
    /// <param name="surface">What it is read on.</param>
    /// <param name="tone">What the color is for.</param>
    /// <returns>The color to draw in.</returns>
    private static Rgb Ladder(Rgb surface, Tone tone)
    {
        lock (Gate)
        {
            if (Answers.TryGetValue((surface, tone), out var found))
            {
                return found;
            }
        }

        var contrast = Shade.Scaled(tone.Contrast, Gutter.Contrast, TextTone.Contrast, surface);
        var hue = tone.Turns ? (tone.Hue + AccentTurn + 360d) % 360d : tone.Hue;
        var answer = Shade.Against(surface, hue, tone.Chroma, contrast);

        lock (Gate)
        {
            Answers[(surface, tone)] = answer;
            Sixteen.TryAdd(answer, tone.Ansi);
        }

        return answer;
    }

    /// <summary>
    /// One color of a design read on a surface of its own rather than on the terminal, for the few that
    /// sit on a color the whole ladder was not written against.
    /// </summary>
    /// <param name="surface">What it is read on.</param>
    /// <param name="tone">What the color is for.</param>
    /// <param name="lowest">The least contrast asked for on that surface.</param>
    /// <param name="highest">The most.</param>
    /// <returns>The color to draw in.</returns>
    private static Rgb Ladder(Rgb surface, Tone tone, double lowest, double highest)
    {
        var contrast = Shade.Scaled(tone.Contrast, lowest, highest, surface);
        var hue = tone.Turns ? (tone.Hue + AccentTurn + 360d) % 360d : tone.Hue;
        var answer = Shade.Against(surface, hue, tone.Chroma, contrast);

        lock (Gate)
        {
            Sixteen.TryAdd(answer, tone.Ansi);
        }

        return answer;
    }

    /// <summary>
    /// Writes down which of the sixteen each color of the design stands for, so a terminal without
    /// 24-bit color is handed the choice the design made rather than the nearest arithmetic answer.
    /// </summary>
    private static void Named()
    {
        lock (Gate)
        {
            foreach (var surface in new[] { Ink, UnlitInk, LitInk, OverlayInk, Chip, Hairline })
            {
                Sixteen.TryAdd(surface, TerminalColor.Default);
            }
        }
    }

    /// <summary>
    /// The one of the terminal's sixteen to fall back on: what the design chose where it chose, and the
    /// nearest by eye where a color was mixed rather than named.
    /// </summary>
    /// <param name="colour">The exact color.</param>
    /// <returns>The one of the sixteen to stand in for it.</returns>
    private static TerminalColor Nearest(Rgb colour)
    {
        if (Sixteen.TryGetValue(colour, out var chosen))
        {
            return chosen;
        }

        var sample = Oklch.Of(colour);
        var closest = TerminalColor.Default;
        var least = double.MaxValue;

        foreach (var (candidate, name) in Plain)
        {
            var against = Oklch.Of(candidate);
            var lightness = sample.Lightness - against.Lightness;
            var chroma = sample.Chroma - against.Chroma;
            var distance = (lightness * lightness * 4d) + (chroma * chroma) + HueGap(sample, against);

            if (distance < least)
            {
                least = distance;
                closest = name;
            }
        }

        return closest;
    }

    /// <summary>How far apart two hues are, counted for nothing where either color is near enough gray.</summary>
    /// <param name="one">The color being matched.</param>
    /// <param name="other">The candidate.</param>
    /// <returns>A number to add to the distance between them.</returns>
    private static double HueGap(Oklch one, Oklch other)
    {
        if (one.Chroma < 0.03d || other.Chroma < 0.03d)
        {
            return 0d;
        }

        var distance = Math.Abs(one.Hue - other.Hue) % 360d;

        return Math.Min(distance, 360d - distance) / 360d;
    }

    private static readonly (Rgb Color, TerminalColor Name)[] Plain =
    [
        (new(0x00, 0x00, 0x00), TerminalColor.Black),
        (new(0x80, 0x00, 0x00), TerminalColor.Red),
        (new(0x00, 0x80, 0x00), TerminalColor.Green),
        (new(0x80, 0x80, 0x00), TerminalColor.Yellow),
        (new(0x00, 0x00, 0x80), TerminalColor.Blue),
        (new(0x80, 0x00, 0x80), TerminalColor.Magenta),
        (new(0x00, 0x80, 0x80), TerminalColor.Cyan),
        (new(0xC0, 0xC0, 0xC0), TerminalColor.White),
        (new(0x80, 0x80, 0x80), TerminalColor.BrightBlack),
        (new(0xFF, 0x00, 0x00), TerminalColor.BrightRed),
        (new(0x00, 0xFF, 0x00), TerminalColor.BrightGreen),
        (new(0xFF, 0xFF, 0x00), TerminalColor.BrightYellow),
        (new(0x00, 0x00, 0xFF), TerminalColor.BrightBlue),
        (new(0xFF, 0x00, 0xFF), TerminalColor.BrightMagenta),
        (new(0x00, 0xFF, 0xFF), TerminalColor.BrightCyan),
        (new(0xFF, 0xFF, 0xFF), TerminalColor.BrightWhite),
    ];

    /// <summary>
    /// One surface and the text on it. Every color here is read against this surface rather than against
    /// the terminal, since a color worked out for one background says nothing about how it reads on another.
    /// </summary>
    /// <param name="background">The background this coat is worn over.</param>
    /// <param name="own">Whether that background is the terminal's own, which is left unpainted.</param>
    public sealed class Coat(Rgb background, bool own = false)
    {
        /// <summary>Primary text: a file name, a dialog title, what was typed.</summary>
        public TermColor Text => field ??= Paint(On(TextTone), Fill);

        /// <summary>The same, said louder — the folder you are in, the title of a dialog.</summary>
        public TermColor Strong => field ??= Paint(On(TextTone), Fill, TextStyle.Bold);

        /// <summary>Text that is not the point but is still read.</summary>
        public TermColor Second => field ??= Paint(On(SideText), Fill);

        /// <summary>Sizes, counts, everything that qualifies a name.</summary>
        public TermColor Meta => field ??= Paint(On(Qualifier), Fill);

        /// <summary>Hints, the parent row, a plain file's tag.</summary>
        public TermColor Hint => field ??= Paint(On(HintTone), Fill);

        /// <summary>Column heads and the small capitals that label a section.</summary>
        public TermColor Label => field ??= Paint(On(LabelTone), Fill);

        /// <summary>A date, or a count on the panel that is not being worked in.</summary>
        public TermColor Trace => field ??= Paint(On(TraceTone), Fill);

        /// <summary>Line numbers, the tag of a file worth ignoring, a hint not needed yet.</summary>
        public TermColor Ghost => field ??= Paint(On(GhostTone), Fill);

        /// <summary>The gutter at rest.</summary>
        public TermColor Sleeping => field ??= Paint(On(Gutter), Fill);

        /// <summary>The accent as text: a caret, a sort arrow, the key of the moment.</summary>
        public TermColor Accent => field ??= Paint(On(AccentLoud), Fill);

        /// <summary>The accent as text, said louder.</summary>
        public TermColor AccentStrong => field ??= Paint(On(AccentLoud), Fill, TextStyle.Bold);

        /// <summary>A host name or a remote path.</summary>
        public TermColor Remote => field ??= Paint(On(Distance), Fill);

        /// <summary>A host name or a remote path, said louder.</summary>
        public TermColor RemoteStrong => field ??= Paint(On(Distance), Fill, TextStyle.Bold);

        /// <summary>A file that is locked, or a job that finished with problems.</summary>
        public TermColor Warning => field ??= Paint(On(Caution), Fill);

        /// <summary>A job that finished.</summary>
        public TermColor Success => field ??= Paint(On(PeaceText), Fill);

        /// <summary>The name of a marked file, on the tinted band its row gets.</summary>
        public TermColor MarkName => field ??= Paint(OnBand(AccentSoft), Band);

        /// <summary>Everything else in a marked row, which is tinted with it.</summary>
        public TermColor MarkMeta => field ??= Paint(OnBand(Qualifier), Band);

        /// <summary>The band itself, for the width the row does not write on.</summary>
        public TermColor MarkRow => field ??= Paint(OnBand(TextTone), Band);

        /// <summary>The rule between two bands of this surface.</summary>
        public TermColor Rule => field ??= Paint(RuleInk, Fill);

        /// <summary>What to fill with, which is nothing at all where the terminal's own is showing.</summary>
        private Rgb? Fill => own ? null : background;

        /// <summary>
        /// The band a marked row gets: this surface put one step of lightness away from itself and turned
        /// to the accent's hue, so it is found by lightness wherever the terminal happens to be.
        /// </summary>
        public Rgb Band => field == default ? field = Banded() : field;

        private Rgb Banded()
        {
            var surface = Oklch.Of(background);
            var step = Contrast.IsDark(background) ? BandStep : -BandStep;

            return (surface with
            {
                Lightness = Math.Clamp(surface.Lightness + step, 0d, 1d),
                Hue = Oklch.Of(Crimson).Hue,
                Chroma = Math.Max(surface.Chroma, BandTint),
            }).ToRgb();
        }

        private Rgb On(Tone tone) => Ladder(background, tone);

        private Rgb OnBand(Tone tone) => Ladder(Band, tone, Qualifier.Contrast, TextTone.Contrast);

        private Rgb RuleInk => background == UnlitInk
            ? HairlineDim
            : background == OverlayInk || background == Chip
                ? HairlineOverlay
                : Hairline;
    }
}
