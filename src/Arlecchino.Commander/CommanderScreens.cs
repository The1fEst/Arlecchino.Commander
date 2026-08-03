using System;
using Arlecchino.Commander.Views;
using Arlecchino.Hosting;
using Arlecchino.Navigation;

namespace Arlecchino.Commander;

/// <summary>
///     The screens this application draws in place of the framework's own, and the frame it draws them
///     inside.
///     The framework brings a few screens — the keys, the notifications, the file picker — and they are
///     drawn in bordered boxes, which is the one thing this design does not use. Taking a route over is
///     how an application says so, and it is the same call the framework registers its own with, so there
///     is nothing to keep in step: whichever is registered last is the one that answers.
///     The layout goes in here rather than at the one place the application is started from, because there
///     are three of those — the application, the headless frame a screenshot is taken from, and the test
///     host — and a screen that wears the band in front of a person and not under test is worse than no
///     test at all.
/// </summary>
public static class CommanderScreens
{
    /// <summary>Takes over the framework routes this application draws for itself.</summary>
    /// <param name="builder">What the application is being built with.</param>
    /// <returns>The same builder.</returns>
    public static ArlecchinoBuilder UseCommanderScreens(this ArlecchinoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddView<NotesView>(Routes.Notifications.Name)
            .UseLayout<LayoutView>();
    }
}
