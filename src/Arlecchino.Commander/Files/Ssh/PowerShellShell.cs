using System;
using System.Diagnostics;

namespace Arlecchino.Commander.Files.Ssh;

/// <summary>Windows PowerShell or PowerShell Core, set as the default shell.</summary>
public sealed class PowerShellShell : WindowsShell
{
    /// <summary>The one of these there is.</summary>
    public static readonly PowerShellShell Instance = new();

    /// <summary>
    /// Removes the folder without <c>-Force</c>, which would take read-only and hidden items too.
    /// <c>-Recurse</c> stops the prompt about a folder that holds things.
    /// </summary>
    /// <param name="path">The folder, as SFTP spells it.</param>
    /// <returns>The command.</returns>
    public override string Sweep(string path) =>
        $"Remove-Item -LiteralPath {Quote(path)} -Recurse";

    /// <inheritdoc/>
    public override string Link(string path, string target) =>
        $"New-Item -ItemType HardLink -Path {Quote(path)} -Target {Quote(target)}";

    /// <inheritdoc/>
    public override string Within(string folder, string command) =>
        $"Set-Location -LiteralPath {Quote(folder)}; {command}";

    /// <inheritdoc/>
    public override void Hand(ProcessStartInfo started, string command)
    {
        started.FileName = "powershell.exe";
        started.ArgumentList.Add("-NoProfile");
        started.ArgumentList.Add("-Command");
        started.ArgumentList.Add(command);
    }

    /// <inheritdoc/>
    public override string Quote(string path) =>
        $"'{Local(path).Replace("'", "''", StringComparison.Ordinal)}'";
}
