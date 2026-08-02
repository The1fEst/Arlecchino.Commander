using Arlecchino.Commander.Files;
using Xunit;

namespace Arlecchino.Commander.Tests;

/// <summary>
/// A connection typed as one line. Everything a session needs is in it, including the password when
/// somebody put one there, so what is read out of the pieces has to be exactly what was written.
/// </summary>
public sealed class LinksTests
{
    [Fact]
    public void EveryPieceOfAnSftpLinkIsRead()
    {
        var connection = Links.Parse("sftp://someone:secret@example.org:2222/srv/files");

        Assert.Equal(Protocol.Sftp, connection.Protocol);
        Assert.Equal("example.org", connection.Host);
        Assert.Equal(2222, connection.Port);
        Assert.Equal("someone", connection.User);
        Assert.Equal("secret", connection.Password);
        Assert.Equal("/srv/files", connection.Path);
    }

    [Fact]
    public void AnFtpLinkIsReadAsFtp()
    {
        var connection = Links.Parse("ftp://someone@example.org/pub");

        Assert.Equal(Protocol.Ftp, connection.Protocol);
        Assert.Equal("/pub", connection.Path);
    }

    [Fact]
    public void LeftUnsaidThePortIsTheOneTheProtocolUses()
    {
        Assert.Equal(Connection.PortFor(Protocol.Sftp), Links.Parse("sftp://someone@example.org/").Port);
        Assert.Equal(Connection.PortFor(Protocol.Ftp), Links.Parse("ftp://someone@example.org/").Port);
    }

    [Fact]
    public void LeftUnsaidTheFolderIsTheTop()
    {
        Assert.Equal(RemotePaths.Root, Links.Parse("sftp://someone@example.org").Path);
    }

    [Fact]
    public void WithNoPasswordThereIsNoPassword()
    {
        Assert.Equal("", Links.Parse("sftp://someone@example.org/srv").Password);
    }

    /// <summary>
    /// A password with a slash or an at sign in it is written escaped, and has to come back out as the
    /// password rather than as part of the address.
    /// </summary>
    [Fact]
    public void WhatWasEscapedComesBackAsItWasWritten()
    {
        var connection = Links.Parse("sftp://some%40one:pass%2Fword@example.org/srv");

        Assert.Equal("some@one", connection.User);
        Assert.Equal("pass/word", connection.Password);
        Assert.Equal("example.org", connection.Host);
    }
}
