using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Arlecchino.Commander.Files;

public static class Connector
{
    /// <summary>
    /// Opens a connection and answers on the drawing thread. Signing in is several round trips and is
    /// waited on rather than run on a thread of its own; what comes back is posted to the frame, since
    /// that is the only thread allowed to change what is on screen.
    ///
    /// The failure callback is told whether the server turned the credentials down rather than being
    /// unreachable, because only then is asking for a password worth anything.
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
                    ? SftpSource.Connect(wanted)
                    : (IFileSource)await FtpSource.ConnectAsync(wanted, CancellationToken.None).ConfigureAwait(false);

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
