using System;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Rendering.Text;

namespace Arlecchino.Commander.Model;

/// <summary>
/// How a folder is written where there is no room for all of it. The panel and the command line both name
/// the folder the panel is looking at, and they name it the same way.
/// </summary>
public static class Paths
{
    private const int Floor = 12;

    /// <summary>
    /// The path with the home folder written as a tilde. Only for the disk: a tilde on a server would
    /// stand for the account the session was opened under, which is not what was asked.
    /// </summary>
    /// <param name="source">Where the folder lives.</param>
    /// <param name="folder">The folder.</param>
    /// <returns>The path, shortened at the front when it started at home.</returns>
    public static string Homed(IFileSource source, string folder)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(folder);

        if (source.IsRemote)
        {
            return folder;
        }

        var home = source.Home;

        return home.Length > 1 && folder.StartsWith(home, StringComparison.Ordinal)
            ? "~" + folder[home.Length..]
            : folder;
    }

    /// <summary>
    /// The path cut to the room there is. What goes is the head, since the folder you are in is the end
    /// of the path.
    /// </summary>
    /// <param name="source">Where the folder lives.</param>
    /// <param name="folder">The folder.</param>
    /// <param name="room">How many cells there are.</param>
    /// <returns>The path, with an ellipsis in place of whatever was dropped.</returns>
    public static string Shortened(IFileSource source, string folder, int room)
    {
        var homed = Homed(source, folder);
        var wanted = Math.Max(Floor, room);

        if (TextWidth.Of(homed) <= wanted)
        {
            return homed;
        }

        var tail = homed[^(wanted - 1)..];
        var cut = tail.IndexOfAny(['/', '\\']);

        return "…" + (cut < 0 ? tail : tail[cut..]);
    }
}
