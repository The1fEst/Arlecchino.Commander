using System;
using System.IO;
using System.Threading;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Views;
using Arlecchino.Navigation;
using Arlecchino.State;
using Arlecchino.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Arlecchino.Commander.Tests;

/// <summary>
/// The application wired up the way it wires itself, drawing into a terminal in memory. The options
/// matter as much as the services: given the framework's defaults instead of Commander's, the hints box
/// lands over the right panel and the test reads a screen the application never shows anyone.
/// </summary>
public sealed class ScreenApp : IDisposable
{
    private const string Loading = "loading…";
    private const int Attempts = 200;
    private const int PollMilliseconds = 10;

    private readonly ArlecchinoTestHost _host;

    public ScreenApp(ViewRoute start, int width = 100, int height = 30)
    {
        Folder = Directory.CreateTempSubdirectory("commander-screen").FullName;

        _host = new(width, height, builder =>
        {
            CommanderOptions.Apply(builder.Options);
            builder.Services.AddSingleton<IHostApplicationLifetime>(new Lifetime());

            builder
                .AddGeneratedViews()
                .AddGeneratedStores()
                .AddGeneratedCommands()
                .UseMouse()
                .StartAt(start);
        });

        Panels.Start(Folder, Folder);
    }

    /// <summary>A folder of its own, gone when the test is.</summary>
    public string Folder { get; }

    private ScreenGrid Screen => _host.Screen;

    public Panels Panels => _host.Services.GetRequiredService<Panels>();

    public Runner Runner => _host.Services.GetRequiredService<Runner>();

    public Finder Finder => _host.Services.GetRequiredService<Finder>();

    public Remote Remote => _host.Services.GetRequiredService<Remote>();

    public Operations Operations => _host.Services.GetRequiredService<Operations>();

    public Navigator Navigator => _host.Navigator;

    public ArlecchinoState State => _host.State;

    public string Frame() => _host.Frame();

    public string[] FrameLines() => _host.FrameLines();

    public void Press(ConsoleKey key, bool shift = false, bool alt = false, bool control = false) =>
        _host.Press(key, shift, alt, control);

    public void Type(string text) => _host.Type(text);

    /// <summary>
    /// Draws frames until something is so, or gives up. Work that finishes off the drawing thread is
    /// posted back to it and runs as a frame is built, so waiting here means drawing rather than
    /// sleeping.
    /// </summary>
    /// <param name="done">What is being waited for.</param>
    /// <returns><c>true</c> when it became so.</returns>
    public bool Until(Func<bool> done)
    {
        ArgumentNullException.ThrowIfNull(done);

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            Frame();

            if (done())
            {
                return true;
            }

            Thread.Sleep(PollMilliseconds);
        }

        return false;
    }

    /// <summary>
    /// Draws frames until no panel is still reading. A folder is read off the drawing thread, so the
    /// first frame of a fresh application says <c>loading…</c> where the files will be — a test that
    /// reads that frame is looking at the screen from before the disk answered.
    /// </summary>
    /// <exception cref="TimeoutException">The reading never finished.</exception>
    public void Settled()
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            if (!Frame().Contains(Loading, StringComparison.Ordinal))
            {
                return;
            }

            Thread.Sleep(PollMilliseconds);
        }

        throw new TimeoutException("The panels never finished reading.");
    }

    /// <summary>Draws frames until some text is on screen, for what arrives after a read.</summary>
    /// <param name="text">What to wait for.</param>
    /// <returns><c>true</c> when it appeared.</returns>
    public bool Shows(string text) => Until(() => Frame().Contains(text, StringComparison.Ordinal));

    /// <summary>A file with something in it, for the tests that need one.</summary>
    /// <param name="name">What to call it.</param>
    /// <param name="text">What to put in it.</param>
    /// <returns>The full path.</returns>
    public string Write(string name, string text)
    {
        var path = Path.Combine(Folder, name);
        File.WriteAllText(path, text);

        return path;
    }

    /// <summary>The style in force where some text starts, wherever the view put it.</summary>
    /// <param name="text">What to look for.</param>
    /// <returns>The escape sequence that cell carries, or an empty string when it is not on screen.</returns>
    public string StyleOf(string text)
    {
        for (var row = 0; row < Screen.Height; row++)
        {
            var at = Screen.Line(row).IndexOf(text, StringComparison.Ordinal);

            if (at >= 0)
            {
                return Screen.StyleAt(row, at);
            }
        }

        return "";
    }

    public void Dispose()
    {
        _host.Dispose();
        Directory.Delete(Folder, true);
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
