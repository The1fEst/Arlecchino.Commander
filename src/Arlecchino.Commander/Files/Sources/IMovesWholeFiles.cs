using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Arlecchino.Commander.Files.Sources;

/// <summary>
/// A source that can move a whole file itself, keeping several requests in flight where a stream sends one
/// and waits. It is handed a stream rather than a path, so the other end can be anything.
/// </summary>
public interface IMovesWholeFiles
{
    /// <summary>Writes a whole file here, out of a stream.</summary>
    /// <param name="reading">Where the bytes come from, read to its end.</param>
    /// <param name="target">The path to write.</param>
    /// <param name="token">Gives up the transfer.</param>
    /// <returns>A task that finishes when the file has been written.</returns>
    Task SendAsync(Stream reading, string target, CancellationToken token);

    /// <summary>Reads a whole file from here, into a stream.</summary>
    /// <param name="source">The path to read.</param>
    /// <param name="writing">Where the bytes go.</param>
    /// <param name="token">Gives up the transfer.</param>
    /// <returns>A task that finishes when the file has been read.</returns>
    Task FetchAsync(string source, Stream writing, CancellationToken token);
}
