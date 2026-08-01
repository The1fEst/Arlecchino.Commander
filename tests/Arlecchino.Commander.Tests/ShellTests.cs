using System;
using System.Diagnostics;
using Arlecchino.Commander.Files;
using Xunit;

namespace Arlecchino.Commander.Tests;

public sealed class ShellTests
{
    /// <summary>
    /// The one command that must not be written the same way twice: <c>rm -f</c> walks past a
    /// read-only file, and a delete that takes what it was not asked to is worse than one that fails.
    /// </summary>
    [Fact]
    public void NothingIsRemovedByForce()
    {
        Assert.DoesNotContain("-f ", PosixShell.Instance.Sweep("/tmp/x"), StringComparison.Ordinal);
        Assert.DoesNotContain("-rf", PosixShell.Instance.Sweep("/tmp/x"), StringComparison.Ordinal);
        Assert.DoesNotContain("-Force", PowerShellShell.Instance.Sweep("/C:/tmp/x"), StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>rmdir /s</c> takes a read-only file with the rest and exits nought either way, and
    /// <c>cmd.exe</c> has no switch that changes either. So it offers nothing and the tree is walked.
    /// </summary>
    [Fact]
    public void TheWindowsCommandShellOffersNoSweepAtAll()
    {
        Assert.Null(WindowsCommandShell.Instance.Sweep("/C:/tmp/x"));
        Assert.Null(ForeignShell.Instance.Sweep("/tmp/x"));
    }

    [Fact]
    public void EachShellRemovesInItsOwnWords()
    {
        Assert.Equal("rm -r -- '/tmp/x'", PosixShell.Instance.Sweep("/tmp/x"));
        Assert.Equal(
            @"Remove-Item -LiteralPath 'C:\tmp\x' -Recurse",
            PowerShellShell.Instance.Sweep("/C:/tmp/x"));
    }

    /// <summary>
    /// A quote inside a path ends the quoting unless it is escaped, and the shells do not agree on
    /// how. Getting this wrong hands the rest of the name to the shell as commands.
    /// </summary>
    [Fact]
    public void AQuoteInsideAPathIsEscapedTheWayEachShellWants()
    {
        Assert.Equal(@"rm -r -- '/tmp/it'\''s'", PosixShell.Instance.Sweep("/tmp/it's"));
        Assert.Equal(
            @"Remove-Item -LiteralPath 'C:\it''s' -Recurse",
            PowerShellShell.Instance.Sweep("/C:/it's"));
    }

    /// <summary>
    /// SFTP reports a Windows path as <c>/C:/Users/…</c>, which neither Windows shell will take.
    /// </summary>
    [Fact]
    public void AWindowsPathLosesTheLeadingSlashAndTurnsItsSlashesRound()
    {
        Assert.Equal(@"cd /d ""C:\Users\fEst"" && dir", WindowsCommandShell.Instance.Within("/C:/Users/fEst", "dir"));
        Assert.Equal(@"mklink /h ""C:\a"" ""C:\b""", WindowsCommandShell.Instance.Link("/C:/a", "/C:/b"));
    }

    [Fact]
    public void ACommandIsRunWhereThePanelIsLooking()
    {
        Assert.Equal("cd '/var/log' && ls", PosixShell.Instance.Within("/var/log", "ls"));
        Assert.Equal(
            @"Set-Location -LiteralPath 'C:\logs'; dir",
            PowerShellShell.Instance.Within("/C:/logs", "dir"));
    }

    /// <summary>
    /// A shell nobody recognised takes no shortcuts: the command goes over as it was typed, since
    /// wrapping it in a dialect that may not be the server's would break what does work.
    /// </summary>
    [Fact]
    public void AShellNobodyRecognisedChangesNothing()
    {
        Assert.Equal("ls", ForeignShell.Instance.Within("/var/log", "ls"));
        Assert.Null(ForeignShell.Instance.Link("/a", "/b"));
    }

    [Fact]
    public void EachShellIsToldApartByWhatItAnswers()
    {
        Assert.IsType<PosixShell>(Shell.Ask(Answers(("uname -s", ("Linux", 0)))));
        Assert.IsType<PowerShellShell>(Shell.Ask(Answers(
            ("uname -s", ("not recognized", 1)),
            ("$PSVersionTable.PSEdition", ("Core", 0)))));
        Assert.IsType<WindowsCommandShell>(Shell.Ask(Answers(
            ("uname -s", ("not recognized", 1)),
            ("$PSVersionTable.PSEdition", ("", 1)),
            ("echo %COMSPEC%", (@"C:\Windows\system32\cmd.exe", 0)))));
        Assert.IsType<ForeignShell>(Shell.Ask(Answers(("uname -s", ("", 1)))));
    }

    /// <summary>
    /// A shell that does not know <c>uname</c> says so in words rather than by failing, and on some
    /// servers it says so with a nought exit status — so the words are what decides.
    /// </summary>
    [Fact]
    public void AComplaintAboutUnameIsNotTakenForAnAnswer()
    {
        Assert.IsType<ForeignShell>(Shell.Ask(Answers(("uname -s", ("'uname' is not recognized", 0)))));
    }

    /// <summary>
    /// <c>cmd.exe</c> takes the command as one raw string behind <c>/s /c</c>, which is the only
    /// spelling it reads back unchanged; a POSIX shell wants it as an argument of its own.
    /// </summary>
    [Fact]
    public void ACommandLineIsHandedOverTheWayEachShellTakesIt()
    {
        var windows = new ProcessStartInfo();

        WindowsCommandShell.Instance.Hand(windows, "dir \"a b\"");

        Assert.Equal("cmd.exe", windows.FileName);
        Assert.Equal("/s /c \"dir \"a b\"\"", windows.Arguments);
        Assert.Empty(windows.ArgumentList);

        var posix = new ProcessStartInfo();

        PosixShell.Instance.Hand(posix, "ls 'a b'");

        Assert.Equal(["-c", "ls 'a b'"], posix.ArgumentList);
        Assert.Empty(posix.Arguments);
    }

    private static Func<string, (string Output, int Status)> Answers(
        params (string Command, (string Output, int Status) Answer)[] scripted) =>
        question =>
        {
            foreach (var (command, answer) in scripted)
            {
                if (question == command)
                {
                    return answer;
                }
            }

            return ("", 1);
        };
}
