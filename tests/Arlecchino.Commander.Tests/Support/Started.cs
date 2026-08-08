using System.IO;
using Arlecchino.Commander.Views;

namespace Arlecchino.Commander.Tests.Support;

/// <summary>
/// The applications the screen tests start from. Every one of them wants the same two files and the same
/// folder to look at. A test that spells that out again is a test with four lines of scenery in front of
/// the one line it is about.
/// </summary>
internal static class Started
{
    /// <summary>
    /// The panels open on a folder of their own, holding two files and a folder, read and drawn.
    /// </summary>
    /// <param name="width">How wide the terminal is.</param>
    /// <returns>The application, for the test to dispose.</returns>
    public static ScreenApp Showing(int width = 130)
    {
        var app = new ScreenApp(ViewKind.Commander, width);

        app.Write("alpha.txt", "one");
        app.Write("beta.txt", "two");
        Directory.CreateDirectory(Path.Combine(app.Folder, "nested"));

        app.Sessions.Start(app.Folder, app.Folder);
        app.Settled();

        return app;
    }

    /// <summary>An application of a given width with a given number of tabs open, settled and drawn.</summary>
    /// <param name="width">How wide the terminal is.</param>
    /// <param name="tabs">How many tabs to open.</param>
    /// <returns>The application, for the test to dispose.</returns>
    public static ScreenApp Tabbed(int width, int tabs)
    {
        var app = new ScreenApp(ViewKind.Commander, width);

        app.Settled();

        for (var opened = 1; opened < tabs; opened++)
        {
            app.Sessions.Add();
        }

        app.Settled();

        return app;
    }
}
