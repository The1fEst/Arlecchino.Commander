using Arlecchino.Atoms;
using Arlecchino.Commander.Files.Ssh;

namespace Arlecchino.Commander.Stores;

public sealed class Remote : IArlecchinoStore
{
    public Atom<string> Scheme { get; } = new("sftp");

    public Atom<string> Host { get; } = new("");

    public Atom<decimal> Port { get; } = new(22);

    public Atom<string> User { get; } = new("");

    public Atom<string> Password { get; } = new("");

    public Atom<string> Folder { get; } = new("/");

    public Atom<string> KeyFile { get; } = new("");

    public Atom<string> Alias { get; } = new("");

    public Atom<bool> Connecting { get; } = new(false);

    public Atom<string> Failure { get; } = new("");

    public Connection? Ssh { get; set; }

    public void Fill(SshHost host)
    {
        Scheme.Value = "sftp";
        Host.Value = host.HostName;
        Port.Value = host.Port;
        User.Value = host.User;
        KeyFile.Value = host.KeyFile;
    }

    public Connection ToConnection() => new(
        Scheme.Value == "ftp" ? Protocol.Ftp : Protocol.Sftp,
        Host.Value.Trim(),
        (int)Port.Value,
        User.Value.Trim(),
        Password.Value,
        Folder.Value.Trim(),
        KeyFile.Value.Trim());
}
