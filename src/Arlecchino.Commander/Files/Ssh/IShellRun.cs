using System;
using System.Threading;
using System.Threading.Tasks;

namespace Arlecchino.Commander.Files.Ssh;

/// <summary>
/// A command that was started and has not been read to its end. A process on this machine can be killed
/// halfway through, where a command already sent to a server cannot.
/// </summary>
public interface IShellRun : IDisposable
{
    /// <summary>Whether anything can be typed at this run at all.</summary>
    bool Listens { get; }

    /// <summary>Reads it to its end, waiting on it rather than holding a thread through the wait.</summary>
    /// <param name="talk">Where every line it prints goes, and where a question it stops on goes.</param>
    /// <param name="token">Gives up the wait; what was already printed is kept.</param>
    Task ReadAsync(ShellTalk talk, CancellationToken token);

    /// <summary>Sends a line to it, as typing one at a terminal would.</summary>
    /// <param name="line">What to send, without the newline that ends it.</param>
    /// <returns><c>true</c> when it went.</returns>
    bool Say(string line);

    /// <summary>Tells it there is no more input, as <c>Ctrl+D</c> does at a terminal.</summary>
    /// <returns><c>true</c> when there was an input open to close.</returns>
    bool EndInput();

    /// <summary>Stops it, when there is anything to stop.</summary>
    /// <returns>Why it could not be stopped, or an empty string when it was.</returns>
    string Interrupt();
}
