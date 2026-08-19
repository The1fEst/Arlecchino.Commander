using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Hosting;
using Arlecchino.Rendering.Colors;

namespace Arlecchino.Commander;

/// <summary>
///     What the framework is told before the first frame: the paint, the keys it may not have, and the
///     smallest terminal this design is worth drawing in.
/// </summary>
public static class CommanderOptions
{
    /// <summary>
    ///     Sets the screen up the way a file manager wants it: this application's colors, its own
    ///     smallest window, no padding, and the chrome it draws for itself switched off.
    /// </summary>
    /// <param name="options">The options to fill in.</param>
    public static void Apply(ArlecchinoOptions options)
    {
        options.Theme = Skin.Palette;
        options.PaletteForBackground = Worn;
        options.CommandPaletteKey = '\0';
        options.MinimumWidth = 100;
        options.MinimumHeight = 20;
        options.HorizontalPadding = 0;
        options.VerticalPadding = 0;
        options.Hints = HintsShown.Never;
        options.ShowOutputLine = false;
    }

    /// <summary>
    /// Puts the design on over the color the terminal turned out to be, so a light terminal is read on
    /// rather than painted over. The colors keep their hues and how far apart they read.
    /// </summary>
    /// <param name="background">What the terminal draws behind the text.</param>
    /// <returns>The palette to hand the framework.</returns>
    private static ThemePalette Worn(Rgb background)
    {
        Skin.Wear(background);

        return Skin.Palette;
    }
}
