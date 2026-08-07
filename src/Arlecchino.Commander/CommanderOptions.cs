using System;
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
    ///     Sets the screen up the way a file manager wants it. The command palette gives up its key: it
    ///     opens on a typed character, and every character belongs to the command line here — a colon
    ///     most of all, since <c>cd C:\Users</c> cannot be typed without one. Every command is on the key
    ///     screen behind <c>F1</c> anyway.
    ///     The palette goes in here rather than at the one place the application is started from. There are
    ///     three of those: the application, the headless frame a screenshot is taken from, and the test host.
    ///     A screen that is one color under test and another in front of a person is worse than no test at
    ///     all.
    ///     The smallest width is what two panels and the gutter between them need to stay readable, and
    ///     nothing more. The bar of keys used to set it, because ten labels had to fit on one row or the last
    ///     of them were dropped; now the bar carries what does not fit onto a second row instead.
    ///     Both paddings are zero because the frame is not the framework's to draw here. The layout keeps a
    ///     margin of its own around every screen of this application, and the framework's own would sit
    ///     outside it and spend the cells twice.
    /// </summary>
    /// <param name="options">The options to fill in.</param>
    public static void Apply(ArlecchinoOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Theme = Skin.Palette;
        options.CommandPaletteKey = '\0';
        options.MinimumWidth = 100;
        options.MinimumHeight = 20;
        options.HorizontalPadding = 0;
        options.VerticalPadding = 0;
        options.ShowHints = false;
        options.ShowOutputLine = false;
    }
}
