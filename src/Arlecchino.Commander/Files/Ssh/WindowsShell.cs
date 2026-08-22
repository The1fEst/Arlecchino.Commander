namespace Arlecchino.Commander.Files.Ssh;

/// <summary>
/// What the two Windows shells agree on, which is the spelling of a path and nothing else. SFTP
/// reports one as <c>/C:/Users/…</c>, and neither <c>cmd.exe</c> nor PowerShell will take that as it
/// stands.
/// </summary>
public abstract class WindowsShell : Shell
{
    /// <summary>The Windows spelling of a path SFTP reports with a leading slash.</summary>
    /// <param name="path">The path as SFTP spells it.</param>
    /// <returns>The same path with a drive letter and backslashes.</returns>
    protected static string Local(string path)
    {
        var trimmedText = path.StartsWith('/') && path.Length > 2 && path[2] == ':' ? path[1..] : path;

        return trimmedText.Replace('/', '\\');
    }
}
