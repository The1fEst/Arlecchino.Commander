using System;
using System.Threading;
using Arlecchino.Commander.Files;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Views;
using Arlecchino.Hosting;
using Arlecchino.Navigation;
using Arlecchino.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Arlecchino.Commander.Frames;

public static class HeadlessFrame
{
    private const int RemoteSettleMilliseconds = 4000;
    private const int PollInterval = 25;

    /// <summary>
    /// Draws one frame and returns, for a screenshot or a check that needs no terminal. A process
    /// started without a console of its own is told there is no colour, which is right for a log and
    /// wrong for a picture, so <c>ARLECCHINO_COLOR</c> says what to emit regardless.
    /// </summary>
    /// <param name="size">Frame size as <c>columns x rows</c>.</param>
    /// <param name="script">Keys to play before the frame is drawn.</param>
    /// <param name="left">Folder the left panel opens on.</param>
    /// <param name="right">Folder the right panel opens on.</param>
    /// <param name="connect">A link or a <c>~/.ssh/config</c> host to open the left panel on.</param>
    /// <param name="wait">How long to let the network and the background work answer, in milliseconds.</param>
    public static void Render(string size, string script, string left, string right, string connect, int wait)
    {
        Colour();

        var services = new ServiceCollection();

        services.AddSingleton<IHostApplicationLifetime, NullLifetime>();
        services
            .AddArlecchino(CommanderOptions.Apply)
            .AddGeneratedViews()
            .AddGeneratedStores()
            .AddGeneratedCommands()
            .WithoutHostedService();

        using var provider = services.BuildServiceProvider();

        var (width, height) = Size(size);
        provider.GetRequiredService<Surface>().SetFixedSize(width, height);
        var panels = provider.GetRequiredService<Panels>();

        panels.Start(left, right);

        if (connect.Length > 0)
        {
            Attach(panels, provider.GetRequiredService<Remote>(), connect);
        }

        provider.GetRequiredService<Navigator>().Apply(ViewKind.Commander);

        var settle = wait > 0 ? wait : connect.Length > 0 ? RemoteSettleMilliseconds : 0;

        Settle(settle);
        Play(provider, script);
        Settle(settle);

        provider.GetRequiredService<Screen>().DrawOnce();

        Console.WriteLine();
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
        }
        while (DateTime.UtcNow < deadline);

        FrameThread.RunPending(static _ => { });
    }

    private static void Attach(Panels panels, Remote remote, string link)
    {
        var wanted = Links.Parse(link);
        var source = wanted.Protocol == Protocol.Sftp
            ? SftpSource.Connect(wanted)
            : (IFileSource)FtpSource.Connect(wanted);

        if (wanted.Protocol == Protocol.Sftp)
        {
            remote.Ssh = wanted;
        }

        panels.Left.Connect(source, wanted.Path.Length > 0 ? wanted.Path : source.Home);
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

        public void StopApplication()
        {
        }
    }
}
