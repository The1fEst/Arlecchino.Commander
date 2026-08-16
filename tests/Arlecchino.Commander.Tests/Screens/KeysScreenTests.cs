using System;
using Arlecchino.Commander.Tests.Support;
using Arlecchino.Commander.Views;
using Xunit;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
/// The screen listing every key, which is this application's own rather than the framework's. It is
/// opened from the panels, since that is where the table of keys it lists is built.
/// </summary>
public sealed class KeysScreenTests : IDisposable
{
    private readonly ScreenApp _app = new(ViewKind.Commander);

    public void Dispose() => _app.Dispose();

    [Fact]
    public void BothSectionsAreListed()
    {
        Opened();

        var screen = _app.Frame();

        Assert.Contains("EVERYWHERE", screen, StringComparison.Ordinal);
        Assert.Contains("ON THE PANELS", screen, StringComparison.Ordinal);
    }

    /// <summary>
    /// The keys of the panels are the ones the screen itself bound, which reach this screen through the
    /// store rather than through the framework. A key missing here means that handover broke.
    /// </summary>
    [Fact]
    public void TheKeysThePanelsBoundAreOnScreen()
    {
        Opened();

        var screen = _app.Frame();

        Assert.Contains("F5", screen, StringComparison.Ordinal);
        Assert.Contains("Copy", screen, StringComparison.Ordinal);
        Assert.Contains("Delete for good", screen, StringComparison.Ordinal);
    }

    [Fact]
    public void ItIsDrawnInTheBandsEveryOtherScreenWears()
    {
        Opened();

        var screen = _app.Frame();

        Assert.Contains("Keys", screen, StringComparison.Ordinal);
        Assert.Contains("every key this application answers to", screen, StringComparison.Ordinal);
        Assert.Contains("Esc back", screen, StringComparison.Ordinal);
    }

    [Fact]
    public void EscapeGoesBackToThePanels()
    {
        Opened();

        _app.Press(ConsoleKey.Escape);

        Assert.Equal(ViewKind.Commander, _app.Navigator.CurrentRoute);
    }

    private void Opened()
    {
        _app.Settled();
        _app.Press(ConsoleKey.F1);
        _app.Frame();
    }
}
