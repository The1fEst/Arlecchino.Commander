using System;

namespace Arlecchino.Commander.Tests.Carries;

/// <summary>
/// The terminal the try is running in: a real one, opened for it, not one of the application's making.
/// What it is asked and how it is typed at differ by machine, so each is a class of its own.
/// </summary>
internal abstract class RealTerminal
{
    /// <summary>The one this machine has.</summary>
    /// <returns>The terminal, which answers that it does not work where the try was started without one.</returns>
    internal static RealTerminal OfThisMachine()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsTerminal();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacTerminal();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxTerminal();
        }

        return new NoTerminal();
    }

    /// <summary>
    /// Whether there is a real terminal here at all. A try started with none tells that apart from a
    /// handover that went wrong.
    /// </summary>
    internal abstract bool Works { get; }

    /// <summary>
    /// The command that takes the screen: it swaps onto the second screen a terminal keeps, says so,
    /// waits to be told one letter, and ends with <see cref="Program.Answer"/> when told the right one.
    /// </summary>
    internal abstract string Drawing { get; }

    /// <summary>
    /// Everything about the real terminal that the handover must leave as it found it, in one line. It
    /// is written down before and after and the two are compared; what it holds is the machine's affair.
    /// </summary>
    /// <returns>The line.</returns>
    internal abstract string State();

    /// <summary>
    /// Presses a key at the real terminal, as the person watching it would. A machine that refuses a
    /// program typing at its own terminal does nothing here, and the test presses from outside instead.
    /// </summary>
    /// <param name="letter">The key.</param>
    internal abstract void Presses(char letter);
}

/// <summary>
/// A machine with no words here. The try says so and stops, which is what a run on a machine these
/// tests were not written for should do rather than fail.
/// </summary>
internal sealed class NoTerminal : RealTerminal
{
    /// <inheritdoc/>
    internal override bool Works => false;

    /// <inheritdoc/>
    internal override string Drawing => "";

    /// <inheritdoc/>
    internal override string State() => "";

    /// <inheritdoc/>
    internal override void Presses(char letter) { }
}
