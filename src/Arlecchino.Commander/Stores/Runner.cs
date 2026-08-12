using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Atoms;
using Arlecchino.Atoms.Local;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Files.Ssh;
using Arlecchino.State;

namespace Arlecchino.Commander.Stores;

/// <summary>
/// What the command line runs and what came back. It lives outside the screen so a command survives
/// walking off to the output and back, and so the same history is there next time.
/// </summary>
public sealed class Runner : IArlecchinoStore
{
    private const int Kept = 2000;

    private readonly ArlecchinoState _state;

    private IShellRun? _running;

    public Runner(ArlecchinoState state) => _state = state;

    /// <summary>
    /// What the commands have said, the oldest first, trimmed to the newest two thousand lines. A list
    /// atom, so output landing on the drawing thread marks the frame stale by itself.
    /// </summary>
    public LocalAtomsList<string> Lines { get; } = new();

    public List<string> History { get; } = [];

    public string Last { get; private set; } = "";

    public bool IsRunning { get; private set; }

    /// <summary>
    /// Runs a command where the panel is looking — on this machine for a local panel, on the server
    /// over the connection the panel already holds for a remote one.
    /// </summary>
    /// <param name="command">What was typed.</param>
    /// <param name="folder">The folder to run it in.</param>
    /// <param name="source">The panel's source, which decides where it runs.</param>
    /// <param name="finished">Called on the drawing thread once it has ended.</param>
    public void Run(string command, string folder, IFileSource source, Action finished)
    {
        ArgumentNullException.ThrowIfNull(finished);

        if (IsRunning)
        {
            _state.Output = Loc(LocString.SaidCommandRunning);
            return;
        }

        Last = command;
        IsRunning = true;

        Remember(command);
        Lines.Add($"$ {command}");

        _ = Running();

        async Task Running()
        {
            var said = await SayAsync(source, command, folder).ConfigureAwait(false);

            FrameThread.Post(() =>
            {
                IsRunning = false;
                _running = null;

                Lines.Add(said);
                Trim();

                _state.Output = Loc(LocString.SaidCommandDone, command);

                finished();
            });
        }
    }

    /// <summary>Kills what is running, along with anything it started.</summary>
    public void Stop()
    {
        var running = _running;

        if (running is null)
        {
            _state.Output = Loc(LocString.SaidNothingRunning);
            return;
        }

        var refused = running.Interrupt();

        _state.Output = refused.Length == 0 ? Loc(LocString.SaidStopped) : refused;
    }

    public void Clear() => Lines.Clear();

    private void Remember(string command)
    {
        History.Remove(command);
        History.Add(command);
    }

    /// <summary>
    /// Starts the command wherever the panel is looking and reads it to its end. Which of them that
    /// is, the source answers; this knows only that a run can be held on to and stopped.
    /// </summary>
    /// <param name="source">The panel's source.</param>
    /// <param name="command">What was typed.</param>
    /// <param name="folder">The folder to run it in.</param>
    /// <returns>What it said.</returns>
    private async Task<List<string>> SayAsync(IFileSource source, string command, string folder)
    {
        if (source.Start(command, folder) is not { } run)
        {
            return [$"[failed] {Loc(LocString.SaidRunsNoCommands, source.Label)}"];
        }

        _running = run;

        using (run)
        {
            return await run.CollectAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private void Trim()
    {
        if (Lines.Count > Kept)
        {
            Lines.RemoveRange(0, Lines.Count - Kept);
        }
    }
}
