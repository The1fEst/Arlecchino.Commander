using System.Diagnostics;

namespace Arlecchino.Commander.Files.Ssh;

/// <summary>Windows <c>cmd.exe</c>, the stock default shell of OpenSSH on Windows.</summary>
public sealed class WindowsCommandShell : WindowsShell
{
    /// <summary>The one of these there is.</summary>
    public static readonly WindowsCommandShell Instance = new();

    /// <summary>
    /// Answers that there is no one-line way, so the tree is walked instead. <c>rmdir /s</c> takes read-only
    /// files along with the rest, and exits nought even where it failed.
    /// </summary>
    /// <param name="path">The folder, as SFTP spells it.</param>
    /// <returns><c>null</c>, always.</returns>
    public override string? Sweep(string path) => null;

    /// <inheritdoc/>
    public override string Link(string path, string target) => $"mklink /h {Quote(path)} {Quote(target)}";

    /// <inheritdoc/>
    public override string Within(string folder, string command) => $"cd /d {Quote(folder)} && {command}";

    /// <inheritdoc/>
    public override string Quote(string path) => $"\"{Local(path)}\"";

    /// <summary>
    /// Fills in <c>cmd.exe</c> behind <c>/s /c</c>, which is the only spelling it reads back unchanged. The
    /// arguments go over as one raw string, since an escaped one loses a quote.
    /// </summary>
    /// <param name="started">What to fill in.</param>
    /// <param name="command">What the user typed.</param>
    public override void Hand(ProcessStartInfo started, string command)
    {
        started.FileName = "cmd.exe";
        started.Arguments = $"/s /c \"{command}\"";
    }
}
