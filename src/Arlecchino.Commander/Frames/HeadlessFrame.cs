using System;
using System.Threading;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Files.Ssh;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Views;
using Arlecchino.Hosting;
using Arlecchino.Navigation;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Colors;
using Arlecchino.Rendering.Terminals;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Arlecchino.Commander.Frames;

public static class HeadlessFrame
{
    private const int RemoteSettleMilliseconds = 4000;
    private const int LocalSettleMilliseconds = 400;
    private const int PollInterval = 25;
    private const int ClosingMilliseconds = 1600;
    private const char Separator = '';

    /// <summary>
    /// Draws one frame and returns, for a screenshot or a check that needs no terminal. A process started
    /// without a console of its own is told there is no color, so <c>ARLECCHINO_COLOR</c> overrides that.
    /// </summary>
    /// <param name="size">Frame size as <c>columns x rows</c>.</param>
    /// <param name="script">Keys to play before the frame is drawn.</param>
    /// <param name="left">Folder the left panel opens on.</param>
    /// <param name="right">Folder the right panel opens on.</param>
    /// <param name="connect">A link or a <c>~/.ssh/config</c> host to open the left panel on.</param>
    /// <param name="wait">How long to let the network and the background work answer, in milliseconds.</param>
    public static void Render(string size, string script, string left, string right, string connect, int wait)
    {
        using var provider = Open(size, left, right, connect);
        var settle = Settling(wait, connect);

        Settle(settle);
        Play(provider, script);
        Settle(settle);

        provider.GetRequiredService<Screen>().DrawOnce();

        Console.WriteLine();
    }

    /// <summary>
    /// Plays a script and writes a frame wherever it asks for one, each preceded by a record separator and
    /// the milliseconds to hold it. A <c>shot</c> step draws the screen and then lets that many pass.
    /// </summary>
    /// <param name="size">Frame size as <c>columns x rows</c>.</param>
    /// <param name="script">Keys and <c>shot</c> steps to play.</param>
    /// <param name="left">Folder the left panel opens on.</param>
    /// <param name="right">Folder the right panel opens on.</param>
    /// <param name="connect">A link or a <c>~/.ssh/config</c> host to open the left panel on.</param>
    /// <param name="wait">How long to let the network answer before the script starts, in milliseconds.</param>
    public static void Record(string size, string script, string left, string right, string connect, int wait)
    {
        using var provider = Open(size, left, right, connect);

        Settle(Settling(wait, connect));
        Play(provider, script);
        Shot(provider, ClosingMilliseconds);
    }

    private static ServiceProvider Open(string size, string left, string right, string connect)
    {
        Colour();

        var services = new ServiceCollection();

        services.AddSingleton<IHostApplicationLifetime, NullLifetime>();
        services
            .AddArlecchino(CommanderOptions.Apply)
            .AddGeneratedViews()
            .AddGeneratedStores()
            .AddGeneratedCommands()
            .UseCommanderScreens()
            .WithoutHostedService();

        var provider = services.BuildServiceProvider();

        var (width, height) = Size(size);
        provider.GetRequiredService<Surface>().SetFixedSize(width, height);
        var panels = provider.GetRequiredService<Sessions>();

        panels.Start(left, right);

        if (connect.Length > 0)
        {
            Attach(panels, provider.GetRequiredService<Remote>(), connect);
        }

        provider.GetRequiredService<Navigator>().Apply(ViewKind.Commander);

        return provider;
    }

    /// <summary>
    /// How long to let the work behind a frame answer. A folder is read off the drawing thread wherever it
    /// lives, so a frame drawn the instant it is asked for catches two panels saying <c>loading…</c>.
    /// </summary>
    /// <param name="wait">What was asked for, when anything was.</param>
    /// <param name="connect">The link the left panel opens on, when there is one.</param>
    /// <returns>The wait, in milliseconds.</returns>
    private static int Settling(int wait, string connect) =>
        wait > 0 ? wait : connect.Length > 0 ? RemoteSettleMilliseconds : LocalSettleMilliseconds;

    private static void Shot(IServiceProvider provider, int hold)
    {
        Console.Out.Write($"{Separator}{hold}\n");
        provider.GetRequiredService<Screen>().DrawOnce();
        Console.Out.Write('\n');

        Settle(hold);
    }

    private static void Colour()
    {
        TerminalCapabilities.Color = Environment.GetEnvironmentVariable("ARLECCHINO_COLOR")?.ToLowerInvariant() switch
        {
            "truecolor" or "24bit" => ColorSupport.TrueColor,
            "256" or "palette" => ColorSupport.Palette,
            "none" => ColorSupport.None,
            _ => TerminalCapabilities.Color,
        };
    }

    private static void Settle(int milliseconds)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);

        do
        {
            FrameThread.RunPending(static _ => { });
            Thread.Sleep(PollInterval);
        } while (DateTime.UtcNow < deadline);

        FrameThread.RunPending(static _ => { });
    }

    /// <summary>
    /// Connects before a headless frame is drawn. This one waits outright, since there is no screen yet
    /// and nothing else to get on with.
    /// </summary>
    /// <param name="sessions">The panels to attach to.</param>
    /// <param name="remote">Told which session is open.</param>
    /// <param name="link">Where to connect.</param>
    private static void Attach(Sessions sessions, Remote remote, string link)
    {
        var wanted = Links.Parse(link);
        var source = wanted.Protocol == Protocol.Sftp
            ? SftpSource.Connect(wanted)
            : (IFileSource)FtpSource.ConnectAsync(wanted, CancellationToken.None).GetAwaiter().GetResult();

        if (wanted.Protocol == Protocol.Sftp)
        {
            remote.Ssh = wanted;
        }

        sessions.Left.Connect(source, wanted.Path.Length > 0 ? wanted.Path : source.Home);
    }

    private static void Play(IServiceProvider provider, string script)
    {
        if (script.Length == 0)
        {
            return;
        }

        var router = provider.GetRequiredService<InputRouter>();

        foreach (var piece in script.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (piece.StartsWith("wait", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(piece[4..], out var pause))
            {
                Settle(pause);
                continue;
            }

            if (piece.StartsWith("shot", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(piece[4..], out var hold))
            {
                Shot(provider, hold);
                continue;
            }

            router.ProcessKey(KeyScript.One(piece));
            FrameThread.RunPending(static _ => { });
        }
    }

    private static (int Width, int Height) Size(string size)
    {
        var parts = size.Split('x');

        return (parts.Length == 2 && int.TryParse(parts[0], out var columns) ? columns : 120,
            parts.Length == 2 && int.TryParse(parts[1], out var rows) ? rows : 34);
    }

    private sealed class NullLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() { }
    }
}
