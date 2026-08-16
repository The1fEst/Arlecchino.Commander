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
/// What a file is, in three letters and a tone, written where an icon would be. A family of extensions
/// shares a tag; anything else is written as its own extension, and a name without one carries no tag.
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
        if (entry.IsParent)
        {
            return "up";
        }

        if (entry.IsFolder)
        {
            return "dir";
        }

        var name = entry.Name;

        if (Git(name))
        {
            return "git";
        }

        if (Secret(name))
        {
            return "key";
        }

        var extension = Extension(name);

        if (KindTags.Of(extension) is { } known)
        {
            return known;
        }

        return entry.IsExecutable ? "exe" : Written(extension);
    }

    /// <summary>How loudly to draw it.</summary>
    /// <param name="entry">The row being drawn.</param>
    /// <returns>The tone.</returns>
    public static Tone ToneOf(FileEntry entry)
    {
        if (entry.IsParent)
        {
            return Tone.Parent;
        }

        var name = entry.Name;
        var tag = KindTags.Of(Extension(name));

        if (Secret(name) || tag is "key")
        {
            return Tone.Protected;
        }

        if (Machinery(name) || entry.IsHidden || tag is "log")
        {
            return Tone.Ignorable;
        }

        return entry.IsFolder ? Tone.Folder : Tone.Plain;
    }

    private static string Extension(string name) => Path.GetExtension(name).ToLowerInvariant();

    /// <summary>
    /// An extension no family covers, written out as far as the column goes. A longer one is cut rather
    /// than dropped, since the first letters of it still answer what the file is.
    /// </summary>
    /// <param name="extension">The extension, dot and all.</param>
    /// <returns>The letters after the dot, or nothing when there are none.</returns>
    private static string Written(string extension) => extension.Length switch
    {
        < 2 => "",
        < TagWidth => extension[1..],
        _ => extension[1..TagWidth],
    };

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
