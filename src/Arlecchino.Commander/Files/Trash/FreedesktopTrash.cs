using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Arlecchino.Commander.Files.Trash;

/// <summary>
/// The trash the Linux desktops share, which is a pair of folders and a plain-text sidecar rather than
/// anything the system provides: the file itself moves to <c>files/</c>, and a <c>.trashinfo</c> beside
/// it in <c>info/</c> records where it came from and when. That sidecar is the whole of how a file
/// manager later offers to put it back, so writing it is not bookkeeping — it is the feature.
///
/// The sidecar is written first and with the demand that it did not already exist. That is what claims
/// the name: two programs emptying an armful into the trash at once would otherwise agree on a name and
/// one would land on top of the other. Only once the name is ours does the file move.
///
/// Only the trash in the user's home is used. The specification also allows one per mounted volume, and a
/// file on another disk belongs there. But a move to the home trash across a disk boundary stops being a
/// rename and becomes a copy of everything, which is not what somebody pressing delete asked for. So that
/// case is refused rather than served slowly and wrongly.
/// </summary>
public sealed class FreedesktopTrash : Trash
{
    /// <summary>
    /// Characters left as themselves in the recorded path. The sidecar holds a URL-encoded path, and
    /// everything outside this set goes over as percent-escapes.
    /// </summary>
    private const string Unreserved = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.!~*'()/";

    private readonly string _root;

    /// <summary>Points this at a trash folder, which is how a test gets one of its own.</summary>
    /// <param name="root">The folder holding <c>files</c> and <c>info</c>.</param>
    public FreedesktopTrash(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);

        _root = root;
    }

    /// <summary>The one this machine has, or none when there is no home to hang it off.</summary>
    public static Trash Instance { get; } = Discover() is { } root ? new FreedesktopTrash(root) : NoTrash.Instance;

    /// <inheritdoc/>
    public override bool Works => true;

    /// <inheritdoc/>
    public override bool TryPut(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var full = Path.GetFullPath(path);
        var name = Path.GetFileName(full);

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.Combine(_root, "files"));
            Directory.CreateDirectory(Path.Combine(_root, "info"));

            if (Claimed(_root, name, full) is not { } claimed)
            {
                return false;
            }

            return Moved(
                full,
                Path.Combine(_root, "files", claimed),
                Path.Combine(_root, "info", claimed + ".trashinfo"));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                                          NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Moves the thing in, and takes the claim back when the move fails. A sidecar describing a file
    /// that is not there is worse than no sidecar: the trash would show an entry that cannot be
    /// restored and cannot be got rid of.
    /// </summary>
    /// <param name="from">Where it is.</param>
    /// <param name="backing">Where it is going.</param>
    /// <param name="sidecar">The claim to undo if it does not get there.</param>
    /// <returns><c>true</c> when it arrived.</returns>
    private static bool Moved(string from, string backing, string sidecar)
    {
        try
        {
            if (Directory.Exists(from))
            {
                Directory.Move(from, backing);
            }
            else
            {
                File.Move(from, backing);
            }

            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            File.Delete(sidecar);

            return false;
        }
    }

    /// <summary>
    /// Writes the sidecar under a name nobody else has, trying a numbered suffix when the plain name is
    /// taken. Creating it is the claim, so it is created new or not at all.
    /// </summary>
    /// <param name="root">The trash folder.</param>
    /// <param name="name">What the thing is called.</param>
    /// <param name="original">Where it is being taken from.</param>
    /// <returns>The name claimed, or <c>null</c> when too many are taken.</returns>
    private static string? Claimed(string root, string name, string original)
    {
        var body = Sidecar(original);

        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var candidate = attempt == 0
                ? name
                : $"{Path.GetFileNameWithoutExtension(name)}_{attempt.ToString(CultureInfo.InvariantCulture)}{Path.GetExtension(name)}";

            try
            {
                using var claiming = new FileStream(
                    Path.Combine(root, "info", candidate + ".trashinfo"),
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);

                claiming.Write(Encoding.UTF8.GetBytes(body));

                return candidate;
            }
            catch (IOException) when (File.Exists(Path.Combine(root, "info", candidate + ".trashinfo"))) { }
        }

        return null;
    }

    /// <summary>
    /// What goes in the sidecar. The date is local and without a zone, which is what the specification
    /// asks for and what every desktop reading it expects.
    /// </summary>
    /// <param name="original">Where the thing came from.</param>
    /// <returns>The contents of the file.</returns>
    private static string Sidecar(string original) =>
        $"[Trash Info]\nPath={Escaped(original)}\nDeletionDate={DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)}\n";

    /// <summary>URL-encodes a path the way the sidecar wants it, byte by byte.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The path, escaped.</returns>
    private static string Escaped(string path)
    {
        var built = new StringBuilder(path.Length);

        foreach (var b in Encoding.UTF8.GetBytes(path))
        {
            if (b < 0x80 && Unreserved.Contains((char)b, StringComparison.Ordinal))
            {
                built.Append((char)b);

                continue;
            }

            built.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }

        return built.ToString();
    }

    /// <summary>
    /// The trash folder in the user's home, wherever the environment says the data folder is.
    /// </summary>
    /// <returns>The folder, or <c>null</c> when there is no home to hang it off.</returns>
    private static string? Discover()
    {
        if (Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { } data && !string.IsNullOrWhiteSpace(data))
        {
            return Path.Combine(data, "Trash");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return string.IsNullOrWhiteSpace(home) ? null : Path.Combine(home, ".local", "share", "Trash");
    }
}
