using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Hosting;

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
        options.CommandPaletteKey = '\0';
        options.MinimumWidth = 100;
        options.MinimumHeight = 20;
        options.HorizontalPadding = 0;
        options.VerticalPadding = 0;
        options.Hints = HintsShown.Never;
        options.ShowOutputLine = false;
    }
}
