using System;
using Arlecchino.Commander.Files.Ssh;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

public sealed class KnownHostsTests
{
    private const string Key = "Zmlyc3Qga2V5IGJ5dGVzLCBmaWZ0eS1vbmUgb2YgdGhlbSBpbiB0b3RhbCBvayEhISEh";
    private const string Other = "c2Vjb25kIGtleSBieXRlcywgYWxzbyBmaWZ0eS1vbmUgbG9uZywgZGlmZmVyaW5nLi4=";

    [Fact]
    public void AHostWithItsKeyIsKnown()
    {
        var hosts = KnownHosts.Parse([$"example.test ssh-ed25519 {Key}"]);

        Assert.Equal(HostVerdict.Known, hosts.Check("example.test", 22, "ssh-ed25519", Bytes(Key)));
    }

    /// <summary>
    /// The one that matters: the host is the one expected and the key is not. Nothing else in an SSH
    /// exchange tells you a machine in the middle from the one you meant to reach.
    /// </summary>
    [Fact]
    public void AHostWithAnotherKeyHasChanged()
    {
        var hosts = KnownHosts.Parse([$"example.test ssh-ed25519 {Key}"]);

        Assert.Equal(HostVerdict.Changed, hosts.Check("example.test", 22, "ssh-ed25519", Bytes(Other)));
    }

    [Fact]
    public void AHostNobodyWroteDownIsUnknown()
    {
        var hosts = KnownHosts.Parse([$"example.test ssh-ed25519 {Key}"]);

        Assert.Equal(HostVerdict.Unknown, hosts.Check("elsewhere.test", 22, "ssh-ed25519", Bytes(Key)));
        Assert.Equal(HostVerdict.Unknown, KnownHosts.Parse([]).Check("example.test", 22, "ssh-ed25519", Bytes(Key)));
    }

    /// <summary>
    /// A key the file marks revoked is refused even though the host and the key match, which is the
    /// whole point of writing it down.
    /// </summary>
    [Fact]
    public void ARevokedKeyIsRefusedRatherThanAccepted()
    {
        var hosts = KnownHosts.Parse([$"@revoked example.test ssh-ed25519 {Key}"]);

        Assert.Equal(HostVerdict.Revoked, hosts.Check("example.test", 22, "ssh-ed25519", Bytes(Key)));
    }

    [Fact]
    public void OneLineMaySpeakForSeveralNames()
    {
        var hosts = KnownHosts.Parse([$"first.test,second.test,10.0.0.1 ssh-ed25519 {Key}"]);

        Assert.Equal(HostVerdict.Known, hosts.Check("second.test", 22, "ssh-ed25519", Bytes(Key)));
        Assert.Equal(HostVerdict.Known, hosts.Check("10.0.0.1", 22, "ssh-ed25519", Bytes(Key)));
    }

    /// <summary>A port other than 22 is part of the name, in brackets before it.</summary>
    [Fact]
    public void APortOtherThanTwentyTwoIsPartOfTheName()
    {
        var hosts = KnownHosts.Parse([$"[other.test]:2222 ssh-ed25519 {Key}"]);

        Assert.Equal(HostVerdict.Known, hosts.Check("other.test", 2222, "ssh-ed25519", Bytes(Key)));
        Assert.Equal(HostVerdict.Unknown, hosts.Check("other.test", 22, "ssh-ed25519", Bytes(Key)));
    }

    /// <summary>
    /// These two lines were written by <c>ssh-keygen -H</c> itself, not by an idea of what it writes.
    /// Hashing the names is the default on Debian and its like.
    /// </summary>
    [Fact]
    public void AHashedNameIsMatchedRatherThanSkipped()
    {
        var hosts = KnownHosts.Parse([
            $"|1|aJaFB+3VE1EVhv5o8hqc6kc9m9g=|TrBaFWfm83fKLsqZ+QPNt+3k9W8= ssh-ed25519 {Key}",
            $"|1|QPU369u+cJ3rvHU148uvBD7nfQk=|wckpR6RpDjw4iIL59CXIyhjup2o= ssh-ed25519 {Other}",
        ]);

        Assert.Equal(HostVerdict.Known, hosts.Check("example.test", 22, "ssh-ed25519", Bytes(Key)));
        Assert.Equal(HostVerdict.Known, hosts.Check("other.test", 2222, "ssh-ed25519", Bytes(Other)));
        Assert.Equal(HostVerdict.Changed, hosts.Check("example.test", 22, "ssh-ed25519", Bytes(Other)));
        Assert.Equal(HostVerdict.Unknown, hosts.Check("third.test", 22, "ssh-ed25519", Bytes(Key)));
    }

    [Fact]
    public void CommentsAndBlanksAndRubbishAreSkipped()
    {
        var hosts = KnownHosts.Parse([
            "# a comment",
            "",
            "   ",
            "not enough columns",
            $"example.test ssh-ed25519 {Key}",
        ]);

        Assert.Equal(1, hosts.Count);
        Assert.Equal(HostVerdict.Known, hosts.Check("example.test", 22, "ssh-ed25519", Bytes(Key)));
    }

    /// <summary>A key of one type says nothing about a host's key of another.</summary>
    [Fact]
    public void AKeyOfAnotherTypeIsNotTheOneWrittenDown()
    {
        var hosts = KnownHosts.Parse([$"example.test ssh-ed25519 {Key}"]);

        Assert.Equal(HostVerdict.Unknown, hosts.Check("example.test", 22, "rsa-sha2-512", Bytes(Key)));
    }

    [Fact]
    public void AMissingFileIsAnEmptyOneRatherThanAFault()
    {
        Assert.Equal(0, KnownHosts.Read("C:/nowhere/at/all/known_hosts").Count);
    }

    [Fact]
    public void TheLineItWouldAddIsWrittenTheWayOpenSshWritesIt()
    {
        Assert.Equal(
            $"example.test ssh-ed25519 {Key}",
            KnownHosts.Line("example.test", 22, "ssh-ed25519", Bytes(Key)));

        Assert.Equal(
            $"[other.test]:2222 ssh-ed25519 {Key}",
            KnownHosts.Line("other.test", 2222, "ssh-ed25519", Bytes(Key)));
    }

    [Fact]
    public void ARefusalSaysWhatToDoAboutIt()
    {
        var second = new HostCheck();
        var unknown = new HostCheck();

        second.Refuse(HostVerdict.Changed, "example.test", "ssh-ed25519", "abc");
        unknown.Refuse(HostVerdict.Unknown, "example.test", "ssh-ed25519", "abc");

        Assert.Contains("ssh-keygen -R example.test", second.Refusal, StringComparison.Ordinal);
        Assert.Contains("ssh example.test", unknown.Refusal, StringComparison.Ordinal);
        Assert.Empty(new HostCheck().Refusal);
    }

    private static byte[] Bytes(string key) => Convert.FromBase64String(key);
}
