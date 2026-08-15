using Arlecchino.Commander.Views;
using Arlecchino.Hosting;
using Arlecchino.Navigation;

namespace Arlecchino.Commander;

/// <summary>
///     The screens this application draws in place of the framework's own, and the frame it draws them
///     inside. The layout is registered here so that all three ways of starting the application get it.
/// </summary>
public static class CommanderScreens
{
    /// <summary>Takes over the framework routes this application draws for itself.</summary>
    /// <param name="builder">What the application is being built with.</param>
    /// <returns>The same builder.</returns>
    public static ArlecchinoBuilder UseCommanderScreens(this ArlecchinoBuilder builder)
    {
        return builder
            .AddView<NotesView>(Routes.Notifications.Name)
            .UseLayout<LayoutView>();
    }
}
