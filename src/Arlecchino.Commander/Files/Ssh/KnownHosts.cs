using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Files.Ssh;

/// <summary>
/// Why a connection was turned away, when it was. The refusal itself happens inside the library, which
/// raises a connection error saying nothing useful — so the reason is kept here and put in its place.
/// </summary>
public sealed class HostCheck
{
    /// <summary>What to tell whoever asked, or an empty string when nothing was refused.</summary>
    public string Refusal { get; private set; } = "";

    /// <summary>Records why the key was refused.</summary>
    /// <param name="verdict">What the file said.</param>
    /// <param name="host">The host that presented it.</param>
    /// <param name="kind">The key type.</param>
    /// <param name="fingerprint">The fingerprint, as OpenSSH prints it.</param>
    public void Refuse(HostVerdict verdict, string host, string kind, string fingerprint) =>
        Refusal = verdict switch
        {
            HostVerdict.Changed => Loc(LocString.HostKeyChanged, kind, host, fingerprint),
            HostVerdict.Revoked => Loc(LocString.HostKeyRevoked, kind, host, fingerprint),
            _ => Loc(LocString.HostKeyUnknown, kind, host, fingerprint),
        };
}

/// <summary>What the file had to say about a host that just presented a key.</summary>
public enum HostVerdict
{
    /// <summary>The host is in the file and this is its key.</summary>
    Known,

    /// <summary>Nothing in the file is about this host.</summary>
    Unknown,

    /// <summary>The host is in the file and this is not its key.</summary>
    Changed,

    /// <summary>The key is in the file and marked as one to refuse.</summary>
    Revoked,
}

/// <summary>
/// The <c>known_hosts</c> file OpenSSH keeps, read the way it writes it: one entry a line, naming the hosts,
/// the kind of key and the key. A host may be plain, comma-separated, <c>[name]:port</c>, or hashed.
/// </summary>
public sealed class KnownHosts
{
    private const string HashMark = "|1|";

    private readonly List<Entry> _entries;

    private KnownHosts(List<Entry> entries) => _entries = entries;

    /// <summary>Where OpenSSH keeps it.</summary>
    public static string Path => System.IO.Path.Combine(Listing.Home(), ".ssh", "known_hosts");

    /// <summary>How many entries were read.</summary>
    public int Count => _entries.Count;

    /// <summary>Reads the file, or hands back an empty one when there is none.</summary>
    /// <param name="path">Which file.</param>
    /// <returns>What it said.</returns>
    public static KnownHosts Read(string path)
    {
        try
        {
            return Parse(File.ReadAllLines(path));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                                          ArgumentException or NotSupportedException)
        {
            return new([]);
        }
    }

    /// <summary>Reads lines already in hand, which is what the tests hold it to.</summary>
    /// <param name="lines">The lines of the file.</param>
    /// <returns>What they said.</returns>
    public static KnownHosts Parse(IEnumerable<string> lines)
    {
        var entries = new List<Entry>();

        foreach (var line in lines)
        {
            if (Entry.Of(line) is { } entry)
            {
                entries.Add(entry);
            }
        }

        return new(entries);
    }

    /// <summary>
    /// Says what the file thinks of a key just presented. A host is looked up by every name it might
    /// be written under, and the answer turns on whether any entry about it holds this key.
    /// </summary>
    /// <param name="host">The host as it was connected to.</param>
    /// <param name="port">The port it was connected on.</param>
    /// <param name="kind">The key type, as <c>ssh-ed25519</c> and the like.</param>
    /// <param name="key">The key itself.</param>
    /// <returns>The verdict.</returns>
    public HostVerdict Check(string host, int port, string kind, byte[] key)
    {
        var about = _entries.Where(entry => entry.IsAbout(host, port)).ToArray();

        if (about.Any(entry => entry.Revoked && entry.Holds(key)))
        {
            return HostVerdict.Revoked;
        }

        if (about.Any(entry => !entry.Revoked && entry.Kind == kind && entry.Holds(key)))
        {
            return HostVerdict.Known;
        }

        return about.Any(entry => !entry.Revoked && entry.Kind == kind)
            ? HostVerdict.Changed
            : HostVerdict.Unknown;
    }

    /// <summary>
    /// The line that would be added for a host, written the way OpenSSH writes it. The name is left in the
    /// clear, since hashing it is the file owner's choice to make.
    /// </summary>
    /// <param name="host">The host as it was connected to.</param>
    /// <param name="port">The port it was connected on.</param>
    /// <param name="kind">The key type.</param>
    /// <param name="key">The key itself.</param>
    /// <returns>The line, without its newline.</returns>
    public static string Line(string host, int port, string kind, byte[] key)
    {
        var named = port is 22 or 0 ? host : $"[{host}]:{port.ToString(CultureInfo.InvariantCulture)}";

        return $"{named} {kind} {Convert.ToBase64String(key)}";
    }

    private sealed record Entry(string[] Names, string Kind, byte[] Key, bool Revoked)
    {
        /// <summary>
        /// Whether this entry holds that key. The bytes are compared rather than the text, since base64 has
        /// more than one spelling for the same bytes.
        /// </summary>
        /// <param name="key">The key presented.</param>
        /// <returns><c>true</c> when they are the same key.</returns>
        public bool Holds(byte[] key) => CryptographicOperations.FixedTimeEquals(Key, key);

        public static Entry? Of(string line)
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed[0] == '#')
            {
                return null;
            }

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var revoked = false;
            var at = 0;

            if (parts.Length > 0 && parts[0].StartsWith('@'))
            {
                revoked = parts[0].Equals("@revoked", StringComparison.Ordinal);
                at = 1;
            }

            if (parts.Length < at + 3)
            {
                return null;
            }

            try
            {
                return new(
                    parts[at].Split(',', StringSplitOptions.RemoveEmptyEntries),
                    parts[at + 1],
                    Convert.FromBase64String(parts[at + 2]),
                    revoked);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        public bool IsAbout(string host, int port)
        {
            var plain = port is 22 or 0 ? host : $"[{host}]:{port.ToString(CultureInfo.InvariantCulture)}";

            foreach (var name in Names)
            {
                if (name.StartsWith(HashMark, StringComparison.Ordinal))
                {
                    if (Hashed(name, plain))
                    {
                        return true;
                    }

                    continue;
                }

                if (name.Equals(plain, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a hashed name is this host. OpenSSH writes <c>|1|salt|hash</c>, where the hash is the name
        /// under HMAC-SHA1 keyed by the salt, so matching one means hashing the name looked for.
        /// </summary>
        /// <param name="name">The name as the file writes it.</param>
        /// <param name="host">The host being looked for.</param>
        /// <returns><c>true</c> when the hash is of that host.</returns>
        [SuppressMessage(
            "Security",
            "CA5350:Do Not Use Weak Cryptographic Algorithms",
            Justification = "The algorithm is not ours to choose: OpenSSH hashes the names in known_hosts " + "with HMAC-SHA1, and a name written by it can only be matched by hashing the same way. " + "Nothing is protected by it — it hides which hosts appear in the file, and what proves " + "the server is the comparison of the key bytes underneath.")]
        private static bool Hashed(string name, string host)
        {
            var parts = name.Split('|');

            if (parts.Length != 4)
            {
                return false;
            }

            try
            {
                using var hmac = new HMACSHA1(Convert.FromBase64String(parts[2]));

                var made = hmac.ComputeHash(Encoding.UTF8.GetBytes(host));

                return CryptographicOperations.FixedTimeEquals(made, Convert.FromBase64String(parts[3]));
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
