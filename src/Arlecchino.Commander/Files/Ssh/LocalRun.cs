using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Arlecchino.Commander.Files.Ssh;

/// <summary>A command running on this machine, as a process that can still be killed.</summary>
public sealed class LocalRun : IShellRun
{
    private readonly string _command;
    private readonly Process? _started;

    /// <summary>Starts it.</summary>
    /// <param name="command">What was typed.</param>
    /// <param name="folder">The folder to run it in.</param>
    public LocalRun(string command, string folder)
    {
        _command = command;
        _started = Shells.Start(command, folder);
    }

    /// <inheritdoc/>
    public bool Listens => _started is { HasExited: false };

    /// <inheritdoc/>
    public async Task ReadAsync(ShellTalk talk, CancellationToken token)
    {
        if (_started is null)
        {
            talk.Prints($"[failed] {_command} could not be started");

            return;
        }

        try
        {
            await Shells.TalkAsync(_started, talk, token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is InvalidOperationException or IOException)
        {
            talk.Prints($"[failed] {error.Message}");
        }
    }

    /// <inheritdoc/>
    public bool Say(string line) => _started is not null && Shells.Say(_started, line);

    /// <inheritdoc/>
    public bool EndInput() => _started is not null && Shells.EndInput(_started);

    /// <inheritdoc/>
    public string Interrupt()
    {
        if (_started is null)
        {
            return "Nothing is running";
        }

        try
        {
            _started.Kill(entireProcessTree: true);

            return "";
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return $"Could not stop it: {error.Message}";
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _started?.Dispose();
}
