using System;
using System.IO;

namespace Arlecchino.Commander.Model;

/// <summary>How loudly a row is drawn, which follows from what the file is rather than what it holds.</summary>
public enum Tone
{
    /// <summary>An ordinary file.</summary>
    Plain,

    /// <summary>A folder, which reads a shade brighter than what is in it.</summary>
    Folder,

    /// <summary>Something with a secret in it: an <c>.env</c>, a private key.</summary>
    Protected,

    /// <summary>Build output, logs, hidden files — there, and not what anyone came for.</summary>
    Ignorable,

    /// <summary>The way out of the folder.</summary>
    Parent,
}

/// <summary>
/// What a file is, in three letters and a tone, written in a column of its own where an icon would be.
/// The tags are a closed set, and a kind that is not one of them reads as <c>txt</c>.
/// </summary>
public static class Kinds
{
    /// <summary>How wide the tag column is drawn, tag and the space after it.</summary>
    public const int TagWidth = 4;

    /// <summary>What to write where an icon would be.</summary>
    /// <param name="entry">The row being drawn.</param>
    /// <returns>Three letters, or fewer.</returns>
    public static string Tag(FileEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.IsParent)
        {
            return "up";
        }

        if (entry.IsFolder)
        {
            return "dir";
        }

        var name = entry.Name;

        return Extension(name) switch
        {
            ".cs" => "cs",
            ".md" or ".markdown" => "md",
            ".json" or ".yml" or ".yaml" or ".toml" or ".ini" or ".conf" or ".config" or ".props" or
                ".targets" or ".editorconfig" => "cfg",
            ".zip" or ".gz" or ".tgz" or ".tar" or ".7z" or ".rar" or ".xz" or ".bz2" => "zip",
            ".log" => "log",
            ".pem" or ".key" or ".pfx" or ".p12" => "key",
            _ when Git(name) => "git",
            _ when Secret(name) => "key",
            _ => "txt",
        };
    }

    /// <summary>How loudly to draw it.</summary>
    /// <param name="entry">The row being drawn.</param>
    /// <returns>The tone.</returns>
    public static Tone ToneOf(FileEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.IsParent)
        {
            return Tone.Parent;
        }

        var name = entry.Name;

        if (Secret(name) || Extension(name) is ".pem" or ".key" or ".pfx" or ".p12")
        {
            return Tone.Protected;
        }

        if (Machinery(name) || entry.IsHidden || Extension(name) is ".log")
        {
            return Tone.Ignorable;
        }

        return entry.IsFolder ? Tone.Folder : Tone.Plain;
    }

    private static string Extension(string name) => Path.GetExtension(name).ToLowerInvariant();

    private static bool Git(string name) =>
        name.StartsWith(".git", StringComparison.OrdinalIgnoreCase) && !Machinery(name);

    private static bool Secret(string name) =>
        name.StartsWith(".env", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("id_", StringComparison.Ordinal) ||
        name.Equals("known_hosts", StringComparison.Ordinal) ||
        name.Equals("authorized_keys", StringComparison.Ordinal);

    private static bool Machinery(string name) =>
        name.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("node_modules", StringComparison.Ordinal) ||
        name.Equals("artifacts", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("coverage", StringComparison.OrdinalIgnoreCase) ||
        name.Equals(".github", StringComparison.OrdinalIgnoreCase) ||
        name.Equals(".vs", StringComparison.OrdinalIgnoreCase) ||
        name.Equals(".idea", StringComparison.OrdinalIgnoreCase);
}
