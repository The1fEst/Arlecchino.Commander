using System;

namespace Arlecchino.Commander.Tests.Carries;

/// <summary>
/// One terminal, opened around one try and typed into once the program inside it has the screen. Each
/// terminal is opened its own way and told what to do its own way, so each is a class of its own.
/// </summary>
internal abstract class OpenedTerminal : IDisposable
{
    /// <summary>What is missing before this terminal can be tried at all.</summary>
    /// <returns>What is missing, or <c>null</c> when nothing is.</returns>
    internal abstract string? Missing();

    /// <summary>Opens it, with the shell in it, with the one try inside that.</summary>
    /// <param name="shell">Which shell.</param>
    /// <param name="command">The try and what to tell it, as a shell takes it.</param>
    /// <returns><c>true</c> when it started.</returns>
    internal abstract bool Opens(string shell, string command);

    /// <summary>Presses a key in it, as the person watching it would.</summary>
    /// <param name="letter">The key.</param>
    internal abstract void Presses(char letter);

    /// <summary>Closes it, whether the try inside it finished or not.</summary>
    public abstract void Dispose();
}
