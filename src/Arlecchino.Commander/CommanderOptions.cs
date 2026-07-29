using System;
using Arlecchino.Hosting;

namespace Arlecchino.Commander;

public static class CommanderOptions
{
    /// <summary>
    /// Sets the screen up the way a file manager wants it. The command palette gives up its key: it
    /// opens on a typed character, and every character belongs to the command line here — a colon
    /// most of all, since <c>cd C:\Users</c> cannot be typed without one. Every command is on the key
    /// screen behind <c>F1</c> anyway.
    /// </summary>
    /// <param name="options">The options to fill in.</param>
    public static void Apply(ArlecchinoOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.CommandPaletteKey = '\0';
        options.MinimumWidth = 80;
        options.MinimumHeight = 18;
        options.HorizontalPadding = 1;
        options.VerticalPadding = 0;
        options.ShowHints = false;
        options.ShowOutputLine = false;
    }
}
