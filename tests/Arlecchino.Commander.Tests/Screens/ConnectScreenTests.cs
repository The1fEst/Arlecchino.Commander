using System;
using Arlecchino.Commander.Views;
using Xunit;
using Arlecchino.Commander.Tests.Support;

namespace Arlecchino.Commander.Tests.Screens;

/// <summary>
/// The form that asks where to connect, drawn without connecting to anything. Nothing here reaches a
/// network: what is being asked is that the fields are on screen, that typing lands in them, and that
/// a failure is shown rather than swallowed.
/// </summary>
public sealed class ConnectScreenTests : IDisposable
{
    private readonly ScreenApp _app = new(ViewKind.Connect);

    public void Dispose() => _app.Dispose();

    [Fact]
    public void TheFieldsAreOnScreen()
    {
        var screen = _app.Frame();

        Assert.Contains("Host", screen, StringComparison.Ordinal);
        Assert.Contains("Folder", screen, StringComparison.Ordinal);
        Assert.Contains("Connect", screen, StringComparison.Ordinal);
    }

    /// <summary>
    /// A field is opened before it is typed into — the form moves with the arrows and Enter opens what
    /// is selected — so this is the whole way round rather than a call to the store.
    /// </summary>
    [Fact]
    public void WhatWasTypedReachesTheFieldItWasOpenedOn()
    {
        _app.Frame();

        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.DownArrow);
        _app.Press(ConsoleKey.Enter);
        _app.Type("example.org");
        _app.Press(ConsoleKey.Enter);

        Assert.Equal("example.org", _app.Remote.Host.Value);
        Assert.Contains("example.org", _app.Frame(), StringComparison.Ordinal);
    }

    [Fact]
    public void AFailureIsSaidRatherThanSwallowed()
    {
        _app.Remote.Failure.Value = "Could not connect: no route to host";

        Assert.Contains("Could not connect", _app.Frame(), StringComparison.Ordinal);
    }

    [Fact]
    public void WhatWasSaidBeforeComesBackIntoTheForm()
    {
        _app.Remote.Host.Value = "kept.example.org";
        _app.Remote.User.Value = "someone";

        var screen = _app.Frame();

        Assert.Contains("kept.example.org", screen, StringComparison.Ordinal);
        Assert.Contains("someone", screen, StringComparison.Ordinal);
    }

    [Fact]
    public void EscapeGoesBackToThePanels()
    {
        _app.Frame();
        _app.Press(ConsoleKey.Escape);

        Assert.Equal(ViewKind.Commander, _app.Navigator.CurrentRoute);
    }
}
