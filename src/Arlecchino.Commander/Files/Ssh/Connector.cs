using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;

namespace Arlecchino.Commander.Files.Ssh;

public static class Connector
{
    /// <summary>
    ///     Opens a connection and answers on the drawing thread. Signing in is several round trips, and
    ///     none of them may happen here: what comes back is posted to the frame, since that is the only
    ///     thread allowed to change what is on screen.
    ///     Signing in over SSH is handed to a thread of its own, because the library has no asynchronous
    ///     way to do it. An <c>async</c> method runs on its caller until the first <c>await</c>, so a
    ///     blocking connect written straight into one still blocks whoever called it — and the caller here
    ///     is the frame loop. The screen froze for the whole handshake, the spinner meant to say the
    ///     connection was being made could not turn, and whatever dialog was last drawn sat there looking
    ///     like the thing that had hung.
    ///     The failure callback is told whether the server turned the credentials down rather than being
    ///     unreachable, because only then is asking for a password worth anything.
    /// </summary>
    /// <param name="wanted">Where to connect and with what.</param>
    /// <param name="landed">Called with the source and the folder to open.</param>
    /// <param name="failed">Called with what went wrong and whether the credentials were refused.</param>
    public static void Start(Connection wanted, Action<IFileSource, string> landed, Action<string, bool> failed)
    {
        ArgumentNullException.ThrowIfNull(wanted);
        ArgumentNullException.ThrowIfNull(landed);
        ArgumentNullException.ThrowIfNull(failed);

        _ = Connecting();

        async Task Connecting()
        {
            try
            {
                var source = wanted.Protocol == Protocol.Sftp
                    ? await Task.Run(() => (IFileSource)SftpSource.Connect(wanted)).ConfigureAwait(false)
                    : await FtpSource.ConnectAsync(wanted, CancellationToken.None).ConfigureAwait(false);

                var folder = wanted.Path.Length > 0 ? wanted.Path : source.Home;

                FrameThread.Post(() => landed(source, folder));
            }
            catch (UnauthorizedAccessException error)
            {
                FrameThread.Post(() => failed(error.Message, true));
            }
            catch (Exception error) when (error is IOException or InvalidOperationException)
            {
                FrameThread.Post(() => failed(error.Message, false));
            }
        }
    }
}
