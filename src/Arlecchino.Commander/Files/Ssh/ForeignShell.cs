using System.Diagnostics;

namespace Arlecchino.Commander.Files.Ssh;

/// <summary>
/// Something none of the others, or a server that would not answer. It takes no shortcuts: a command goes
/// over as it was typed, and a folder is removed the long way, one entry at a time.
/// </summary>
public sealed class ForeignShell : Shell
{
    /// <summary>The one of these there is.</summary>
    public static readonly ForeignShell Instance = new();

    /// <inheritdoc/>
    public override string? Sweep(string path) => null;

    /// <inheritdoc/>
    public override string? Link(string path, string target) => null;

    /// <inheritdoc/>
    public override string Within(string folder, string command) => command;

    /// <inheritdoc/>
    public override string Quote(string path) => path;

    /// <inheritdoc/>
    public override void Hand(ProcessStartInfo started, string command) =>
        PosixShell.Instance.Hand(started, command);
}
