using System;
using System.Diagnostics;
using System.IO;

namespace Arlecchino.Commander.Tests.Carries;

/// <summary>
/// A terminal people install, which keeps a name of its own to be reached by while it runs. The key is
/// sent by asking it through that name, so nothing about the handover is changed by the asking.
/// </summary>
internal sealed class InstalledTerminal : OpenedTerminal
{
    /// <summary>What it is called.</summary>
    internal const string Name = "kitty";

    /// <summary>What its asking half is called, which is a program of its own.</summary>
    private const string Asking = "kitten";

    private string _socket = "";
    private Process? _process;

    /// <inheritdoc/>
    internal override string? Missing() =>
        Along(Name) && Along(Asking) ? null : $"{Name} is not on this machine";

    /// <inheritdoc/>
    internal override bool Opens(string shell, string command)
    {
        _socket = Path.Combine(Path.GetTempPath(), $"arlc-carries-{Environment.ProcessId}-{shell}");

        File.Delete(_socket);

        _process = Processes.Started(
            Name,
            "--listen-on",
            $"unix:{_socket}",
            "-o",
            "allow_remote_control=yes",
            shell,
            "-c",
            command);

        return _process is not null;
    }

    /// <inheritdoc/>
    internal override void Presses(char letter) =>
        _ = Processes.Answered(Asking, "@", "--to", $"unix:{_socket}", "send-text", letter.ToString());

    /// <inheritdoc/>
    public override void Dispose()
    {
        Processes.Ended(_process);

        _process?.Dispose();
        _process = null;
    }

    /// <summary>Whether a program is along the path.</summary>
    /// <param name="program">What it is called.</param>
    /// <returns><c>true</c> when it is on this machine.</returns>
    private static bool Along(string program)
    {
        foreach (var place in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (place.Length > 0 && File.Exists(Path.Combine(place, program)))
            {
                return true;
            }
        }

        return false;
    }
}
