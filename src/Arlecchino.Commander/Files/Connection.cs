namespace Arlecchino.Commander.Files;

public enum Protocol
{
    Sftp,
    Ftp,
}

public sealed record Connection(
    Protocol Protocol,
    string Host,
    int Port,
    string User,
    string Password,
    string Path,
    string KeyFile = "",
    string Alias = "")
{
    public static Connection Empty { get; } = new(Protocol.Sftp, "", 22, "", "", "/");

    public string Scheme => Protocol == Protocol.Sftp ? "sftp" : "ftp";

    /// <summary>
    /// What the panel calls the connection: a host opened by its <c>~/.ssh/config</c> name is called
    /// that, the way <c>ssh</c> itself would, and anything else is spelled out in full.
    /// </summary>
    public string Label => Alias.Length > 0 ? $"{Scheme} {Alias}" : $"{Scheme} {User}@{Host}";

    public static int PortFor(Protocol protocol) => protocol == Protocol.Sftp ? 22 : 21;
}
