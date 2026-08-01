using System.IO;
using Arlecchino.Commander;
using Arlecchino.Commander.Frames;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Views;
using Arlecchino.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var left = args.Length >= 1 && Directory.Exists(args[0]) ? args[0] : Directory.GetCurrentDirectory();
var right = args.Length >= 2 && Directory.Exists(args[1]) ? args[1] : left;

if (args is ["--frame", ..] or ["--tape", ..])
{
    var size = args.Length >= 2 ? args[1] : "120x34";
    var script = Option(args, "--keys") ?? "";
    var leftPanel = Option(args, "--left") ?? Directory.GetCurrentDirectory();
    var rightPanel = Option(args, "--right") ?? Directory.GetCurrentDirectory();
    var connect = Option(args, "--connect") ?? "";
    var wait = int.TryParse(Option(args, "--wait"), out var milliseconds) ? milliseconds : 0;

    if (args[0] == "--tape")
    {
        HeadlessFrame.Record(size, script, leftPanel, rightPanel, connect, wait);
    }
    else
    {
        HeadlessFrame.Render(size, script, leftPanel, rightPanel, connect, wait);
    }

    return;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddArlecchino(CommanderOptions.Apply)
    .AddGeneratedViews()
    .AddGeneratedStores()
    .AddGeneratedCommands()
    .UseMouse()
    .StartAt(ViewKind.Commander);

var host = builder.Build();

host.Services.GetRequiredService<Panels>().Start(left, right);

await host.RunAsync();

static string? Option(string[] arguments, string name)
{
    for (var index = 0; index < arguments.Length - 1; index++)
    {
        if (arguments[index] == name)
        {
            return arguments[index + 1];
        }
    }

    return null;
}
