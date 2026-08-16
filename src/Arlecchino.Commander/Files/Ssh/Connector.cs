using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;

namespace Arlecchino.Commander.Files.Ssh;

public static class Connector
{
    /// <summary>
    /// Opens a connection on a thread of its own, since the library has no asynchronous way to sign in, and
    /// answers on the drawing thread. The failure says whether the credentials were refused.
    /// </summary>
    /// <param name="wanted">Where to connect and with what.</param>
    /// <param name="landed">Called with the source and the folder to open.</param>
    /// <param name="failed">Called with what went wrong and whether the credentials were refused.</param>
    public static void Start(Connection wanted, Action<IFileSource, string> landed, Action<string, bool> failed)
    {
        FrameThread.Post(async () =>
        {
            try
            {
                var source = wanted.Protocol == Protocol.Sftp
                    ? await Task.Run(() => (IFileSource)SftpSource.Connect(wanted))
                    : await FtpSource.ConnectAsync(wanted, CancellationToken.None);

                landed(source, wanted.Path.Length > 0 ? wanted.Path : source.Home);
            }
            catch (UnauthorizedAccessException error)
            {
                failed(error.Message, true);
            }
            catch (Exception error) when (error is IOException or InvalidOperationException)
            {
                failed(error.Message, false);
            }
        });
    }
}
