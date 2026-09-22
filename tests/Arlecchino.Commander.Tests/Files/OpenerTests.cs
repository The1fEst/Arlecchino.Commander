using Arlecchino.Commander.Files.Opening;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// How a file is handed to whatever the machine opens it with, asserted as the start rather than the
/// window. The window belongs to a desktop no test host has, and the start is where the mistakes live.
/// </summary>
public sealed class OpenerTests
{
    private const string SpacedPath = "/home/someone/My Holiday Photos/last summer.jpg";

    [Fact]
    public void TheDesktopHandlerIsHandedTheWholePath()
    {
        var start = new FreedesktopOpener("xdg-open").Starting(SpacedPath);

        Assert.Equal("xdg-open", start.FileName);
        Assert.Equal([SpacedPath], start.ArgumentList);
    }

    /// <summary>A handler wanting a word of its own gets it before the path rather than joined on.</summary>
    [Fact]
    public void AHandlerWithAWordOfItsOwnGetsItFirst()
    {
        var start = new FreedesktopOpener("gio", "open").Starting(SpacedPath);

        Assert.Equal("gio", start.FileName);
        Assert.Equal(["open", SpacedPath], start.ArgumentList);
    }

    /// <summary>
    /// The streams are taken and left unread: a handler writing onto the terminal would write into
    /// the middle of a frame the panels had already drawn.
    /// </summary>
    [Fact]
    public void WhatTheDesktopHandlerSaysIsKeptOffTheScreen()
    {
        var start = new FreedesktopOpener("xdg-open").Starting(SpacedPath);

        Assert.False(start.UseShellExecute);
        Assert.True(start.RedirectStandardOutput);
        Assert.True(start.RedirectStandardError);
    }

    [Fact]
    public void MacOpensThroughTheOneProgramItHas()
    {
        var start = MacOpener.Instance.Starting(SpacedPath);

        Assert.Equal("open", start.FileName);
        Assert.Equal([SpacedPath], start.ArgumentList);
    }

    /// <summary>Windows names the file where a program would go, there being no program to name.</summary>
    [Fact]
    public void WindowsAsksTheShellRatherThanAProgram()
    {
        var start = WindowsOpener.Instance.Starting(SpacedPath);

        Assert.Equal(SpacedPath, start.FileName);
        Assert.True(start.UseShellExecute);
        Assert.Empty(start.ArgumentList);
    }

    /// <summary>A machine with no handler answers before anything is started.</summary>
    [Fact]
    public void AMachineWithNoHandlerOpensNothing()
    {
        Assert.False(NoOpener.Instance.Works);
        Assert.False(NoOpener.Instance.TryOpen(SpacedPath));
    }
}
