using System;
using Arlecchino.Commander.Files.Ssh;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// Reading a question off what a command printed, and spelling <c>sudo</c> so that it asks where it can
/// be answered rather than at a terminal this application has none of.
/// </summary>
public sealed class PromptsTests
{
    [Theory]
    [InlineData("Password: ")]
    [InlineData("[sudo] password for fest:")]
    [InlineData("Enter passphrase for key '/home/fest/.ssh/id_ed25519':")]
    [InlineData("Пароль:")]
    public void ALineACommandStoppedOnIsAQuestion(string pending)
    {
        Assert.True(Prompts.Asks(pending, out var prompt));
        Assert.EndsWith(":", prompt, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("reading the folder")]
    public void AnythingElseIsNot(string pending)
    {
        Assert.False(Prompts.Asks(pending, out _));
    }

    [Fact]
    public void AVeryLongLineIsProseRatherThanAQuestion()
    {
        Assert.False(Prompts.Asks(new string('x', 400) + ":", out _));
    }

    [Theory]
    [InlineData("sudo apt update", "sudo -S apt update")]
    [InlineData("sudo -k apt update", "sudo -S -k apt update")]
    [InlineData("ls | sudo tee /etc/hosts", "ls | sudo -S tee /etc/hosts")]
    [InlineData("cd /tmp && sudo rm -r x", "cd /tmp && sudo -S rm -r x")]
    public void SudoIsToldToReadTheAnswerFromWhereOneCanBeSent(string command, string run)
    {
        Assert.Equal(run, Prompts.Piped(command));
    }

    [Theory]
    [InlineData("sudo -S apt update")]
    [InlineData("sudo -n true")]
    [InlineData("sudo -A visudo")]
    [InlineData("sudo --stdin apt update")]
    public void OneAlreadyToldIsLeftAlone(string command)
    {
        Assert.Equal(command, Prompts.Piped(command));
    }

    [Theory]
    [InlineData("echo sudo")]
    [InlineData("echo 'sudo apt update'")]
    [InlineData("grep sudo /etc/group")]
    public void TheWordSomewhereOtherThanTheFrontIsNotACommand(string command)
    {
        Assert.Equal(command, Prompts.Piped(command));
    }
}
