using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Arlecchino.Commander.Files.Ssh;

/// <summary>
/// A command sent to a server over the session the panel already holds. It is read as it prints and can
/// be answered while it runs, the same as one on this machine; what it cannot be is killed.
/// </summary>
public sealed class RemoteRun : IShellRun
{
    private readonly SftpSource _source;
    private readonly string _command;
    private readonly string _folder;

    private Stream? _input;

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
    public bool Listens => _input is not null;

    /// <inheritdoc/>
    public async Task ReadAsync(ShellTalk talk, CancellationToken token)
    {
        var shell = await _source
            .TalkAsync(_command, _folder, running => SayingAsync(running, talk, token), token)
            .ConfigureAwait(false);

        if (!shell)
        {
            talk.Prints("[failed] the server offered no shell");
        }
    }

    /// <inheritdoc/>
    public bool Say(string line)
    {
        if (_input is not { } input)
        {
            return false;
        }

        try
        {
            input.Write(Encoding.UTF8.GetBytes(line + "\n"));
            input.Flush();

            return true;
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or SshException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public bool EndInput()
    {
        if (_input is not { } input)
        {
            return false;
        }

        _input = null;

        try
        {
            input.Dispose();
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or SshException)
        {
            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public string Interrupt() => "A command on the server cannot be stopped from here";

    /// <inheritdoc/>
    public void Dispose() => EndInput();

    /// <summary>
    /// Runs the command that was opened and reads both of the streams it prints on as they fill. A server
    /// that hands over no streams to read is waited on instead, as every command was before this.
    /// </summary>
    /// <param name="running">The command, opened but not yet started.</param>
    /// <param name="talk">Where the lines and the questions go.</param>
    /// <param name="token">Gives up the wait.</param>
    private async Task SayingAsync(SshCommand running, ShellTalk talk, CancellationToken token)
    {
        var work = running.ExecuteAsync(token);

        try
        {
            if (running.OutputStream is { } output && running.ExtendedOutputStream is { } problem)
            {
                _input = Opened(running);

                await Reading(output, problem, talk, token).ConfigureAwait(false);
                await work.ConfigureAwait(false);
            }
            else
            {
                await work.ConfigureAwait(false);

                foreach (var line in Shells.Split(running.Result + running.Error))
                {
                    talk.Prints(line);
                }
            }
        }
        finally
        {
            EndInput();
        }

        talk.Prints($"[exit {running.ExitStatus ?? -1}]");
    }

    /// <summary>Reads what the command prints on both of its streams until they run dry.</summary>
    /// <param name="output">What it printed.</param>
    /// <param name="problem">What it complained about.</param>
    /// <param name="talk">Where the lines and the questions go.</param>
    /// <param name="token">Gives up the wait.</param>
    private static async Task Reading(Stream output, Stream problem, ShellTalk talk, CancellationToken token)
    {
        using var printing = new StreamReader(output);
        using var complaining = new StreamReader(problem);

        var reading = Shells.ReadAsync(printing, talk, token);
        var listening = Shells.ReadAsync(complaining, talk, token);

        await reading.ConfigureAwait(false);
        await listening.ConfigureAwait(false);
    }

    /// <summary>
    /// The stream a command is typed into, where the server allows one. It is opened once the command is
    /// running, since there is nothing to send to before that.
    /// </summary>
    /// <param name="running">The command.</param>
    /// <returns>The stream, or <c>null</c> when there is none to be had.</returns>
    private static Stream? Opened(SshCommand running)
    {
        try
        {
            return running.CreateInputStream();
        }
        catch (Exception error) when (error is InvalidOperationException or ObjectDisposedException)
        {
            return null;
        }
    }
}
