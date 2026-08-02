using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Arlecchino.Commander.Files.Sources;

namespace Arlecchino.Commander.Files.Ssh;

/// <summary>
/// A command that was started and has not been read to its end. The two sources that can run one
/// differ in what they hand back and there is no pretending otherwise: a process on this machine can
/// be killed halfway through, a command already sent to a server cannot.
/// </summary>
public interface IShellRun : IDisposable
{
    /// <summary>Reads it to its end, waiting on it rather than occupying anybody with the wait.</summary>
    /// <param name="token">Gives up the wait; what was already printed is kept.</param>
    /// <returns>Everything it printed, with how it ended as the last line.</returns>
    Task<List<string>> CollectAsync(CancellationToken token);

    /// <summary>Stops it, when there is anything to stop.</summary>
    /// <returns>Why it could not be stopped, or an empty string when it was.</returns>
    string Interrupt();
}

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
    public async Task<List<string>> CollectAsync(CancellationToken token)
    {
        if (_started is null)
        {
            return [$"[failed] {_command} could not be started"];
        }

        try
        {
            return await Shells.CollectAsync(_started, token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is InvalidOperationException or IOException)
        {
            return [$"[failed] {error.Message}"];
        }
    }

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

/// <summary>
/// A command sent to a server over the session the panel already holds. It is run when it is read,
/// since the shell answers with everything at once; there is nothing to kill afterwards.
/// </summary>
public sealed class RemoteRun : IShellRun
{
    private readonly SftpSource _source;
    private readonly string _command;
    private readonly string _folder;

    /// <summary>Holds what to send.</summary>
    /// <param name="source">The connection to send it over.</param>
    /// <param name="command">What was typed.</param>
    /// <param name="folder">The folder to run it in.</param>
    public RemoteRun(SftpSource source, string command, string folder)
    {
        _source = source;
        _command = command;
        _folder = folder;
    }

    /// <inheritdoc/>
    public async Task<List<string>> CollectAsync(CancellationToken token)
    {
        if (await _source.RunAsync(_command, _folder, token).ConfigureAwait(false) is not { } said)
        {
            return ["[failed] the server offered no shell"];
        }

        return [.. Shells.Split(said.Output), $"[exit {said.Status}]"];
    }

    /// <inheritdoc/>
    public string Interrupt() => "A command on the server cannot be stopped from here";

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}
