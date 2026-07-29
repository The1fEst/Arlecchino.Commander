using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Arlecchino.Atoms;
using Arlecchino.Commander.Files;
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

    private Process? _running;

    public Runner(ArlecchinoState state) => _state = state;

    /// <summary>
    /// What the commands have said, oldest first, trimmed to the newest two thousand lines. A list
    /// atom rather than a list, so output landing on the drawing thread marks the frame stale by
    /// itself, and a trim is one change rather than one per line dropped.
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
            _state.Output = "A command is still running";
            return;
        }

        Last = command;
        IsRunning = true;

        Remember(command);
        Lines.Add($"$ {command}");

        Task.Run(() =>
        {
            var said = source is SftpSource sftp ? Server(sftp, command, folder) : Here(command, folder);

            FrameThread.Post(() =>
            {
                IsRunning = false;
                _running = null;

                Lines.Add(said);
                Trim();

                _state.Output = $"{command} · Ctrl+O reads what it said";

                finished();
            });
        });
    }

    /// <summary>Kills what is running, along with anything it started.</summary>
    public void Stop()
    {
        var running = _running;

        if (running is null)
        {
            _state.Output = "Nothing is running";
            return;
        }

        try
        {
            running.Kill(entireProcessTree: true);
            _state.Output = "Stopped";
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            _state.Output = $"Could not stop it: {error.Message}";
        }
    }

    public void Clear() => Lines.Clear();

    private void Remember(string command)
    {
        History.Remove(command);
        History.Add(command);
    }

    private List<string> Here(string command, string folder)
    {
        if (Shells.Start(command, folder) is not { } started)
        {
            return [$"[failed] {command} could not be started"];
        }

        _running = started;

        try
        {
            return Shells.Collect(started);
        }
        catch (Exception error) when (error is InvalidOperationException or IOException)
        {
            return [$"[failed] {error.Message}"];
        }
    }

    private static List<string> Server(SftpSource source, string command, string folder)
    {
        if (source.Run(command, folder) is not { } said)
        {
            return ["[failed] the server offered no shell"];
        }

        return [.. Shells.Split(said.Output), $"[exit {said.Status}]"];
    }

    private void Trim()
    {
        if (Lines.Count > Kept)
        {
            Lines.RemoveRange(0, Lines.Count - Kept);
        }
    }
}
