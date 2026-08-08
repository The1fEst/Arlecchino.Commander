using System;
using System.IO;
using System.Threading;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Views;
using Arlecchino.Hosting;
using Arlecchino.Input;
using Arlecchino.Navigation;
using Arlecchino.State;
using Arlecchino.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Arlecchino.Commander.Tests.Support;

/// <summary>
///     The application wired up the way it wires itself, drawing into a terminal in memory. The options
///     matter as much as the services: given the framework's defaults instead of Commander's, the hints box
///     lands over the right panel and the test reads a screen the application never shows anyone.
/// </summary>
public sealed class ScreenApp : IDisposable
{
    private const string Loading = "reading";
    private const string Prompt = "❯";
    private const int Attempts = 200;
    private const int PollMilliseconds = 10;

    private readonly ArlecchinoTestHost _host;

    public ScreenApp(ViewRoute start, int width = 130, int height = 30)
    {
        Width = width;
        Folder = Directory.CreateTempSubdirectory("commander-screen").FullName;

        _host = new(width,
            height,
            builder =>
            {
                CommanderOptions.Apply(builder.Options);
                builder.Services.AddSingleton<IHostApplicationLifetime>(new Lifetime());

                builder
                    .AddGeneratedViews()
                    .AddGeneratedStores()
                    .AddGeneratedCommands()
                    .UseMouse()
                    .UseCommanderScreens()
                    .StartAt(start);
            });

        Sessions.Start(Folder, Folder);
    }

    /// <summary>A folder of its own, gone when the test is.</summary>
    public string Folder { get; }

    /// <summary>
    ///     How wide the terminal is. A test that asks whether anything spilled over the edge has to
    ///     compare against this rather than against a number typed into it, or it stops meaning anything
    ///     the day the default changes.
    /// </summary>
    public int Width { get; }

    private ScreenGrid Screen => _host.Screen;

    public Sessions Sessions => _host.Services.GetRequiredService<Sessions>();

    public Runner Runner => _host.Services.GetRequiredService<Runner>();

    public Finder Finder => _host.Services.GetRequiredService<Finder>();

    public Remote Remote => _host.Services.GetRequiredService<Remote>();

    public Operations Operations => _host.Services.GetRequiredService<Operations>();

    public Navigator Navigator => _host.Navigator;

    public ArlecchinoState State => _host.State;

    /// <summary>
    ///     What was last put on the clipboard. The terminal in memory keeps it rather than sending it, so
    ///     what is asserted is the text itself and not merely that something was sent.
    /// </summary>
    public string? Copied => _host.Terminal.Copied;

    public void Dispose()
    {
        _host.Dispose();
        Directory.Delete(Folder, true);
    }

    public string Frame()
    {
        return _host.Frame();
    }

    public string[] FrameLines()
    {
        return _host.FrameLines();
    }

    /// <summary>
    ///     How many rows the top and the bottom of the terminal are given away before a screen of this
    ///     application starts: the frame the framework puts round every view, and the margin the layout
    ///     keeps inside it. Anything here that reads a row by number counts both, and reads them off the
    ///     options the application was built with rather than repeating the numbers.
    /// </summary>
    public int Inset => _host.Services.GetRequiredService<ArlecchinoOptions>().VerticalPadding + LayoutView.Margin;

    /// <summary>The band of tabs as it reads.</summary>
    /// <returns>The row the tabs are drawn on.</returns>
    public string BandLine() => FrameLines()[Inset];

    /// <summary>
    ///     Which row the command line landed on. It is the floor the panels stand on: everything below it
    ///     belongs to the bar of keys, so a bar that grew a row moves the prompt one row up.
    /// </summary>
    /// <returns>The row, or <c>-1</c> when the screen is drawing something else entirely.</returns>
    public int CommandLineRow() => Array.FindIndex(FrameLines(), static line => line.Contains(Prompt, StringComparison.Ordinal));

    /// <summary>
    ///     The bar of actions along the bottom, however many rows it took. A narrow terminal makes the bar
    ///     carry what did not fit onto another row. A test that read one row would be reading half a bar the
    ///     day the width changed, so the rows are given back as one line, joined the way they read.
    /// </summary>
    /// <returns>Everything the bar spells out.</returns>
    public string BarLine() => string.Join(' ', BarLines());

    /// <summary>
    ///     The rows the bar of actions is on, top one first. They are the last rows of the screen with
    ///     anything on them apart from the command line above them, which is the one row that is always
    ///     there and is told apart by the prompt it ends with.
    /// </summary>
    /// <returns>The rows.</returns>
    public string[] BarLines()
    {
        var lines = FrameLines();
        var last = lines.Length - 1 - Inset;
        var first = last;

        while (first > Inset && !lines[first - 1].Contains(Prompt, StringComparison.Ordinal))
        {
            first--;
        }

        return lines[first..(last + 1)];
    }

    public void Press(ConsoleKey key, bool shift = false, bool alt = false, bool control = false)
    {
        var modifiers = (shift ? KeyModifiers.Shift : KeyModifiers.None) |
                        (alt ? KeyModifiers.Alt : KeyModifiers.None) |
                        (control ? KeyModifiers.Control : KeyModifiers.None);

        _host.Press(key, modifiers);
    }

    public void Type(string text)
    {
        _host.Type(text);
    }

    /// <summary>
    ///     Feeds the bytes a terminal really sends, escapes and all, through the reader that recognizes
    ///     them. Pressing a key names it; this is the way to ask what a named key arrives as.
    /// </summary>
    /// <param name="sequence">The characters as the terminal would send them.</param>
    public void ReadFromTerminal(string sequence)
    {
        _host.ReadFromTerminal(sequence);
    }

    public void Click(int row, int column)
    {
        _host.Click(row, column);
    }

    /// <summary>
    ///     Draws frames until something is so, or gives up. Work that finishes off the drawing thread is
    ///     posted back to it and runs as a frame is built, so waiting here means drawing rather than
    ///     sleeping.
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
    ///     Draws frames until no panel is still reading. A folder is read off the drawing thread, so the
    ///     first frame of a fresh application says <c>loading…</c> where the files will be — a test that
    ///     reads that frame is looking at the screen from before the disk answered.
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

    /// <summary>Draws frames until the screen shows some text, for what arrives after a read.</summary>
    /// <param name="text">What to wait for.</param>
    /// <returns><c>true</c> when it appeared.</returns>
    public bool Shows(string text)
    {
        return Until(() => Frame().Contains(text, StringComparison.Ordinal));
    }

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
    /// <returns>The escape sequence that cell carries, or an empty string when the text is not there.</returns>
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

    private sealed class Lifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _stopping = new();

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
            _stopping.Cancel();
        }
    }
}
