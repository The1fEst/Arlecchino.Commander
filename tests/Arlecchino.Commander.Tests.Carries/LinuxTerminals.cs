using System;

namespace Arlecchino.Commander.Tests.Carries;

/// <summary>
/// The terminals this machine opens: a pair of ends made by the test, and one people install. The key is
/// pressed from here, since the try inside changes nothing about the terminal it is being carried in.
/// </summary>
internal sealed class LinuxTerminals : Terminals
{
    /// <summary>
    /// The terminal that needs no one logged in: a pair of ends opened by the test itself. Nothing is
    /// drawn on a screen, so this is the one a build server tries.
    /// </summary>
    internal const string Headless = "headless";

    private OpenedTerminal? _one;
    private string _name = "";

    /// <inheritdoc/>
    internal override string? Missing(string terminal, string shell)
    {
        if (Found(shell) is null)
        {
            return $"{shell} is not on this machine";
        }

        if (One(terminal) is not { } one)
        {
            return $"{terminal} is not a terminal this kind of machine opens";
        }

        if (terminal != Headless && !Shown)
        {
            return $"{terminal} draws itself a window, and there is no screen here to draw one on";
        }

        return one.Missing();
    }

    /// <inheritdoc/>
    internal override bool Opens(string terminal, string shell, string runner, string log) =>
        One(terminal) is { } one &&
        one.Opens(shell, $"{Processes.Quoted(runner)} {Processes.Quoted(log)} {terminal} {shell}");

    /// <inheritdoc/>
    internal override void Presses(string terminal, char letter) => One(terminal)?.Presses(letter);

    /// <inheritdoc/>
    public override void Dispose()
    {
        _one?.Dispose();
        _one = null;
        _name = "";
    }

    /// <summary>
    /// Whether there is a screen here for a window to be drawn on. A machine reached over a connection
    /// has none, and a terminal asked for one there starts and dies before the try inside it says a word.
    /// </summary>
    private static bool Shown =>
        Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") is { Length: > 0 } ||
        Environment.GetEnvironmentVariable("DISPLAY") is { Length: > 0 };

    /// <summary>
    /// The terminal that goes by a name, made the once and kept: what is opened is what is typed into
    /// and what is closed afterward, and one try opens one terminal.
    /// </summary>
    /// <param name="terminal">Which one.</param>
    /// <returns>It, or <c>null</c> when this machine opens no terminal of that name.</returns>
    private OpenedTerminal? One(string terminal)
    {
        if (_name == terminal)
        {
            return _one;
        }

        _one?.Dispose();
        _name = terminal;
        _one = terminal switch
        {
            Headless => new HeadlessTerminal(),
            InstalledTerminal.Name => new InstalledTerminal(),
            _ => null,
        };

        return _one;
    }
}
