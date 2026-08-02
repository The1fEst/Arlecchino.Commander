using System;
using System.IO;
using System.Linq;
using System.Threading;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Views;
using Arlecchino.Rendering;
using Arlecchino.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Arlecchino.Commander.Tests;

/// <summary>
/// The panels as they reach the screen. Everything else here is about parsing what a server or a disk
/// said; this is about what the person in front of the terminal ends up looking at, which until now
/// nothing asserted.
/// </summary>
public sealed class CommanderScreenTests : IDisposable
{
    private readonly string _folder;
    private readonly ArlecchinoTestHost _host;

    public CommanderScreenTests()
    {
        _folder = Directory.CreateTempSubdirectory("commander-screen").FullName;

        File.WriteAllText(Path.Combine(_folder, "alpha.txt"), "one");
        File.WriteAllText(Path.Combine(_folder, "beta.txt"), "two");
        Directory.CreateDirectory(Path.Combine(_folder, "nested"));

        _host = new(100, 30, builder =>
        {
            CommanderOptions.Apply(builder.Options);
            builder.Services.AddSingleton<IHostApplicationLifetime>(new Lifetime());

            builder
                .AddGeneratedViews()
                .AddGeneratedStores()
                .AddGeneratedCommands()
                .UseMouse()
                .StartAt(ViewKind.Commander);
        });

        _host.Services.GetRequiredService<Panels>().Start(_folder, _folder);
    }

    public void Dispose()
    {
        _host.Dispose();
        Directory.Delete(_folder, true);
    }

    [Fact]
    public void BothPanelsShowWhatIsInTheFolder()
    {
        var screen = _host.Frame();

        Assert.Contains("alpha.txt", screen, StringComparison.Ordinal);
        Assert.Contains("beta.txt", screen, StringComparison.Ordinal);
        Assert.Contains("nested", screen, StringComparison.Ordinal);

        Assert.Equal(2, Occurrences(screen, "alpha.txt"));
    }

    /// <summary>
    /// The one thing a frame read as text cannot answer. A folder and a file read the same; what tells
    /// them apart is the colour, and the colour is on the screen rather than in the words.
    /// </summary>
    [Fact]
    public void AFolderIsDrawnInTheColourFoldersGet()
    {
        _host.Frame();

        Assert.Equal(Theme.Accent.Ansi, StyleOf("nested"));
        Assert.Equal(Theme.Default.Ansi, StyleOf("alpha.txt"));
    }

    [Fact]
    public void MarkingAFileRepaintsItInTheColourMarksGet()
    {
        var panels = _host.Services.GetRequiredService<Panels>();

        _host.Frame();

        _host.Press(ConsoleKey.DownArrow);
        _host.Press(ConsoleKey.DownArrow);
        _host.Press(ConsoleKey.Spacebar);
        _host.Frame();

        Assert.True(panels.Left.Marks.Contains("alpha.txt"));
        Assert.Equal(Theme.Warning.Ansi, StyleOf("alpha.txt"));
    }

    [Fact]
    public void MovingTheCursorLeavesTheRestOfTheScreenAlone()
    {
        var before = _host.FrameLines();

        _host.Press(ConsoleKey.DownArrow);

        var after = _host.FrameLines();
        var moved = before.Zip(after).Count(static rows => rows.First != rows.Second);

        Assert.InRange(moved, 0, 4);
        Assert.Equal(before.Length, after.Length);
    }

    [Fact]
    public void EnteringAFolderTakesThePanelIntoIt()
    {
        var panels = _host.Services.GetRequiredService<Panels>();

        _host.Frame();

        _host.Press(ConsoleKey.DownArrow);
        _host.Press(ConsoleKey.Enter);

        var screen = _host.Frame();

        Assert.EndsWith("nested", panels.Left.Folder, StringComparison.Ordinal);
        Assert.Contains("nested", screen, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFunctionKeyBarIsAlongTheBottom()
    {
        var lines = _host.FrameLines();

        Assert.Contains("Quit", lines[^1], StringComparison.Ordinal);
        Assert.Contains("Help", lines[^1], StringComparison.Ordinal);
    }

    /// <summary>The style in force where a name starts, wherever the panel put it.</summary>
    /// <param name="name">The name to look for.</param>
    /// <returns>The escape sequence the cell carries.</returns>
    private string StyleOf(string name)
    {
        var screen = _host.Screen;

        for (var row = 0; row < screen.Height; row++)
        {
            var at = screen.Line(row).IndexOf(name, StringComparison.Ordinal);

            if (at >= 0)
            {
                return screen.StyleAt(row, at);
            }
        }

        return "";
    }

    private static int Occurrences(string screen, string name)
    {
        var found = 0;
        var at = screen.IndexOf(name, StringComparison.Ordinal);

        while (at >= 0)
        {
            found++;
            at = screen.IndexOf(name, at + name.Length, StringComparison.Ordinal);
        }

        return found;
    }

    private sealed class Lifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _stopping = new();

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => _stopping.Cancel();
    }
}
