using System;
using System.IO;

namespace Arlecchino.Commander.Tests.Carries;

/// <summary>
/// The terminals a machine has, how one of them is opened around a program, and how a key is pressed in
/// it afterward. Each kind of machine opens its own and is a class of its own below this one.
/// </summary>
internal abstract class Terminals : IDisposable
{
    /// <summary>The ones this machine has.</summary>
    /// <returns>The terminals, which know nothing at all on a machine these tests were not written for.</returns>
    internal static Terminals OfThisMachine()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsTerminals();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacTerminals();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxTerminals();
        }

        return new NoTerminals();
    }

    /// <summary>
    /// What is missing before this try can be run at all: a terminal this machine does not open, a shell
    /// it does not have, a window with no one logged in to be shown it.
    /// </summary>
    /// <param name="terminal">Which terminal.</param>
    /// <param name="shell">Which shell.</param>
    /// <returns>What is missing, or <c>null</c> when nothing is.</returns>
    internal abstract string? Missing(string terminal, string shell);

    /// <summary>Opens the terminal, with the shell in it, with the one try inside that.</summary>
    /// <param name="terminal">Which terminal.</param>
    /// <param name="shell">Which shell.</param>
    /// <param name="runner">The program that does the one try.</param>
    /// <param name="log">Where that program writes what it found.</param>
    /// <returns><c>true</c> when the terminal started.</returns>
    internal abstract bool Opens(string terminal, string shell, string runner, string log);

    /// <summary>
    /// Presses a key in the terminal that was opened, once the program inside it has the screen and is
    /// waiting to be told something. A machine whose try presses its own key does nothing here.
    /// </summary>
    /// <param name="terminal">Which terminal.</param>
    /// <param name="letter">The key.</param>
    internal abstract void Presses(string terminal, char letter);

    /// <summary>Closes whatever was opened for the try, whether the try got that far or not.</summary>
    public abstract void Dispose();

    /// <summary>
    /// Whether this is a build server rather than a machine with someone at it. A terminal that draws
    /// itself a window has nowhere to draw one here, and asking it to open one answers nothing at all.
    /// </summary>
    private protected static bool IsBuildServer =>
        Environment.GetEnvironmentVariable("CI") is { Length: > 0 };

    /// <summary>Where a program is along the path.</summary>
    /// <param name="program">What it is called.</param>
    /// <returns>The path to it, or <c>null</c> when it is not on this machine.</returns>
    private protected static string? Found(string program)
    {
        foreach (var place in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (place.Length > 0 && File.Exists(Path.Combine(place, program)))
            {
                return Path.Combine(place, program);
            }
        }

        return null;
    }
}

/// <summary>
/// A machine with no terminals named here. Every try is passed over, which is what a run on a machine
/// these tests were not written for should do rather than fail.
/// </summary>
internal sealed class NoTerminals : Terminals
{
    /// <inheritdoc/>
    internal override string Missing(string terminal, string shell) =>
        $"{terminal} is not a terminal this kind of machine opens";

    /// <inheritdoc/>
    internal override bool Opens(string terminal, string shell, string runner, string log) => false;

    /// <inheritdoc/>
    internal override void Presses(string terminal, char letter) { }

    /// <inheritdoc/>
    public override void Dispose() { }
}
