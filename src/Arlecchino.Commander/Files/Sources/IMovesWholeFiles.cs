using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Arlecchino.Commander.Files.Sources;

/// <summary>
/// A source that can move a whole file itself, rather than have one read out of it a block at a time.
///
/// It is here for the one thing a stream cannot do over a network: keep several requests in flight. A
/// stream sends one request and waits for the answer before sending the next, so a file crosses at one
/// request per round trip however large the requests are. Over a link a tenth of a second wide that is a
/// fixed ceiling no buffer size can lift. A source moving the whole file has the next request already gone
/// when the last one answers, and on such a link that is the whole difference.
///
/// The work is handed a stream rather than a path on purpose: the other end is then anything at all —
/// a disk, another server — and only the end that can pipeline has to know how.
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
