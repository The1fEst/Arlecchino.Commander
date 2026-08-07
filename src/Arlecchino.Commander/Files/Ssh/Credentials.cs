using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arlecchino.Commander.Model;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Arlecchino.Commander.Files.Ssh;

public static class Credentials
{
    private static readonly string[] DefaultKeys = ["id_ed25519", "id_ecdsa", "id_rsa", "id_dsa"];

    public static ConnectionInfo For(Connection connection)
    {
        var methods = new List<AuthenticationMethod>();

        foreach (var file in KeyFiles(connection))
        {
            if (Load(file, connection.Password) is { } key)
            {
                methods.Add(new PrivateKeyAuthenticationMethod(connection.User, key));
            }
        }

        if (methods.Count == 0 || connection.Password.Length > 0)
        {
            methods.Add(new PasswordAuthenticationMethod(connection.User, connection.Password));
        }

        var info = new ConnectionInfo(connection.Host, connection.Port, connection.User, [.. methods]);

        Narrow(info);

        return info;
    }

    /// <summary>
    /// Leaves only what a server worth connecting to still offers. What goes is not obscure: single
    /// DES in three passes, every cipher in CBC mode, key exchange and signatures over SHA-1, and the
    /// Diffie-Hellman groups small enough to have been broken in public. A library offers them so
    /// that something from 2005 still answers; a file manager holding somebody's password does not
    /// have to take that trade.
    ///
    /// It costs nothing in reach. Everything left is what OpenSSH has offered by default for years,
    /// and the list keeps the NIST curves behind the modern ones so that a server without them still
    /// has something to agree on.
    /// </summary>
    /// <param name="info">The connection to narrow.</param>
    private static void Narrow(ConnectionInfo info)
    {
        Keep(info.KeyExchangeAlgorithms,
        [
            "curve25519-sha256",
            "curve25519-sha256@libssh.org",
            "ecdh-sha2-nistp256",
            "ecdh-sha2-nistp384",
            "ecdh-sha2-nistp521",
            "diffie-hellman-group-exchange-sha256",
            "diffie-hellman-group18-sha512",
            "diffie-hellman-group16-sha512",
            "diffie-hellman-group14-sha256",
        ]);

        Keep(info.HostKeyAlgorithms,
        [
            "ssh-ed25519-cert-v01@openssh.com",
            "ecdsa-sha2-nistp256-cert-v01@openssh.com",
            "ecdsa-sha2-nistp384-cert-v01@openssh.com",
            "ecdsa-sha2-nistp521-cert-v01@openssh.com",
            "rsa-sha2-512-cert-v01@openssh.com",
            "rsa-sha2-256-cert-v01@openssh.com",
            "ssh-ed25519",
            "ecdsa-sha2-nistp256",
            "ecdsa-sha2-nistp384",
            "ecdsa-sha2-nistp521",
            "rsa-sha2-512",
            "rsa-sha2-256",
        ]);

        Keep(info.Encryptions,
        [
            "chacha20-poly1305@openssh.com",
            "aes256-gcm@openssh.com",
            "aes128-gcm@openssh.com",
            "aes256-ctr",
            "aes192-ctr",
            "aes128-ctr",
        ]);

        Keep(info.HmacAlgorithms,
        [
            "hmac-sha2-256-etm@openssh.com",
            "hmac-sha2-512-etm@openssh.com",
            "hmac-sha2-256",
            "hmac-sha2-512",
        ]);
    }

    private static void Keep<T>(IDictionary<string, T> offered, string[] wanted)
    {
        foreach (var name in offered.Keys.Where(name => !wanted.Contains(name, StringComparer.Ordinal)).ToArray())
        {
            offered.Remove(name);
        }
    }

    /// <summary>
    /// Holds the server to the key it showed before. Nothing else in the exchange says who answered:
    /// the encryption is agreed with whoever is on the other end, so without this a machine sitting
    /// in the middle gets the password and everything after it.
    ///
    /// A server is trusted when <c>~/.ssh/known_hosts</c> already says that key is its. Anything else
    /// is refused rather than asked about — a question here would arrive while the panel is loading,
    /// and the honest answer to "should I trust this?" is not one to guess at. The first connection
    /// to a server is made with <c>ssh</c>, which asks properly and writes the file.
    /// </summary>
    /// <param name="client">The client about to connect.</param>
    /// <param name="connection">Where it is connecting.</param>
    /// <returns>What to ask afterward for the reason, when it refused.</returns>
    public static HostCheck Watch(BaseClient client, Connection connection)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(connection);

        var check = new HostCheck();
        var known = KnownHosts.Read(KnownHosts.Path);

        client.HostKeyReceived += (_, presented) =>
        {
            var verdict = known.Check(
                connection.Host,
                connection.Port,
                presented.HostKeyName,
                presented.HostKey);

            presented.CanTrust = verdict == HostVerdict.Known;

            if (!presented.CanTrust)
            {
                check.Refuse(verdict, connection.Host, presented.HostKeyName, presented.FingerPrintSHA256);
            }
        };

        return check;
    }

    private static List<string> KeyFiles(Connection connection)
    {
        if (connection.KeyFile.Length > 0)
        {
            return File.Exists(connection.KeyFile) ? [connection.KeyFile] : [];
        }

        var found = new List<string>();
        var folder = Path.Combine(Listing.Home(), ".ssh");

        foreach (var name in DefaultKeys)
        {
            var path = Path.Combine(folder, name);

            if (File.Exists(path))
            {
                found.Add(path);
            }
        }

        return found;
    }

    private static PrivateKeyFile? Load(string file, string passphrase)
    {
        try
        {
            return passphrase.Length == 0 ? new(file) : new PrivateKeyFile(file, passphrase);
        }
        catch (Exception error) when (error is IOException or SshException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
