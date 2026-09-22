using System.Diagnostics;

namespace Arlecchino.Commander.Files.Opening;

/// <summary>
/// The handler macOS ships, which is one program and is always there. It reads what the file is bound
/// to in Launch Services and puts the window up itself.
/// </summary>
public sealed class MacOpener : Opener
{
    /// <summary>The one of these there is.</summary>
    public static readonly MacOpener Instance = new();

    /// <inheritdoc/>
    public override ProcessStartInfo Starting(string path) => new("open")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        ArgumentList = { path },
    };
}
