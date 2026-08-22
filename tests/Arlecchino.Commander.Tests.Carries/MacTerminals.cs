using System.Runtime.Versioning;

namespace Arlecchino.Commander.Tests.Carries;

/// <summary>
/// The terminals this machine opens: a pair of ends made by the test, the one it ships with, and one
/// people install. The key is pressed from here, since this machine refuses a program that types at
/// its own terminal.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MacTerminals : Terminals
{
    /// <summary>
    /// The terminal that needs no one logged in: a pair of ends opened by the test itself. Nothing is
    /// drawn on a screen, so this is the one a build server tries.
    /// </summary>
    internal const string Headless = "headless";

    /// <summary>What the machine calls the session a screen is attached to.</summary>
    private const string AquaSession = "Aqua";

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
            return $"{terminal} is not a terminal this machine opens";
        }

        if (terminal != Headless && IsBuildServer)
        {
            return $"{terminal} draws itself a window, and a build server has no screen to draw one on";
        }

        if (terminal != Headless && Processes.Answered("launchctl", "managername") != AquaSession)
        {
            return $"{terminal} draws itself a window, and no one is logged in here to be shown one";
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
            ShippedTerminal.Name => new ShippedTerminal(),
            InstalledTerminal.Name => new InstalledTerminal(),
            _ => null,
        };

        return _one;
    }
}
