using System.Collections.Generic;
using System.Text;
using Arlecchino.Commander.Files.Tty;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// Telling the moment a command asks for the screen from everything else it prints. Nothing about the
/// name it was typed under is looked at: the only thing asked is what it wrote.
/// </summary>
public sealed class ClaimsTests
{
    /// <summary>Reads a whole string and answers with what came of it.</summary>
    /// <param name="text">What the command printed.</param>
    /// <returns>The letters kept, and whether the screen was claimed along the way.</returns>
    private static (string Letters, bool Claimed) Read(string text)
    {
        var claims = new Claims();
        var letters = new StringBuilder();
        var claimed = false;

        foreach (var letter in Encoding.UTF8.GetBytes(text))
        {
            switch (claims.Takes(letter))
            {
                case Sign.Letter:
                    letters.Append((char)letter);

                    break;

                case Sign.Screen:
                    claimed = true;

                    break;

                default:
                    break;
            }
        }

        return (letters.ToString(), claimed);
    }

    [Fact]
    public void PlainOutputClaimsNothing()
    {
        var (letters, claimed) = Read("hello\nthere\n");

        Assert.False(claimed);
        Assert.Equal("hello\nthere\n", letters);
    }

    /// <summary>
    /// Color is the commonest thing a command writes to a terminal, and a great many commands write it
    /// and go on printing lines. Taken for a claim, it would hand the screen to nearly everything.
    /// </summary>
    [Fact]
    public void ColorIsNotAClaim()
    {
        var (letters, claimed) = Read("\e[32mgreen\e[0m\n");

        Assert.False(claimed);
        Assert.Equal("green\n", letters);
    }

    [Fact]
    public void TitlesAndOtherStringsAreNotAClaim()
    {
        var (letters, claimed) = Read("\e]0;a title\aafter\n");

        Assert.False(claimed);
        Assert.Equal("after\n", letters);
    }

    /// <summary>The second screen a terminal keeps is what most full-screen programs swap themselves onto.</summary>
    /// <param name="sequence">What the command printed.</param>
    [Theory]
    [InlineData("\e[?1049h")]
    [InlineData("\e[?1047h")]
    [InlineData("\e[?47h")]
    [InlineData("\e[?1049;1h")]
    public void SwappingScreensIsAClaim(string sequence) => Assert.True(Read(sequence).Claimed);

    [Fact]
    public void TurningTheMouseOnIsAClaim() => Assert.True(Read("\e[?1000h").Claimed);

    /// <summary>
    /// Not every full-screen program takes the second screen: some draw over the one they were given.
    /// They ask the keyboard for the arrows instead, and then draw where they please.
    /// </summary>
    /// <param name="sequence">What the command printed.</param>
    [Theory]
    [InlineData("\e[?1h")]
    [InlineData("\e[H")]
    [InlineData("\e[12;40H")]
    [InlineData("\e[2J")]
    public void DrawingOnTheScreenItWasGivenIsAClaimToo(string sequence) => Assert.True(Read(sequence).Claimed);

    /// <summary>
    /// A count drawn over itself hides the cursor, wipes the line and writes it again. Taken for a claim,
    /// that would take the screen away from every command showing how far along it is.
    /// </summary>
    /// <param name="sequence">What the command printed.</param>
    [Theory]
    [InlineData("\e[K")]
    [InlineData("\e[2K")]
    [InlineData("\e[1A")]
    [InlineData("\e[?25l")]
    [InlineData("\e[?2004h")]
    [InlineData("\e[0J")]
    public void DrawingOverTheLineItIsOnIsNotAClaim(string sequence) => Assert.False(Read(sequence).Claimed);

    /// <summary>
    /// A question a program asks the terminal is a claim as much as any drawing is. The answer can only
    /// come from a terminal, and a roll of text left to give it would leave the program waiting for good.
    /// </summary>
    /// <param name="sequence">What the command printed.</param>
    [Theory]
    [InlineData("\e[6n")]
    [InlineData("\e[c")]
    [InlineData("\e[>c")]
    public void AQuestionToTheTerminalIsAClaim(string sequence) => Assert.True(Read(sequence).Claimed);

    /// <summary>Leaving the screen and answering a question are not asking for anything.</summary>
    /// <param name="sequence">What the command printed.</param>
    [Theory]
    [InlineData("\e[?1049l")]
    [InlineData("\e[0n")]
    public void GivingTheScreenBackIsNotAClaim(string sequence) => Assert.False(Read(sequence).Claimed);

    /// <summary>
    /// What a command writes arrives in whatever lengths the terminal hands over, so a claim can be split
    /// down the middle. It is read across the join, or it is not read at all.
    /// </summary>
    [Fact]
    public void AClaimSplitAcrossTwoMouthfulsIsStillRead()
    {
        var claims = new Claims();
        var claimed = false;

        foreach (var mouthful in new List<string> { "text\e[?10", "49h" })
        {
            foreach (var letter in Encoding.UTF8.GetBytes(mouthful))
            {
                claimed |= claims.Takes(letter) is Sign.Screen;
            }
        }

        Assert.True(claimed);
    }

    /// <summary>
    /// The instruction is kept whole, since the program sent it to a terminal and the terminal is about
    /// to be handed to it. Dropping it would leave the program drawing on the screen it did not ask for.
    /// </summary>
    [Fact]
    public void TheClaimItselfIsKeptToBePassedOn()
    {
        var claims = new Claims();

        foreach (var letter in Encoding.UTF8.GetBytes("\e[?1049h"))
        {
            claims.Takes(letter);
        }

        Assert.Equal("\e[?1049h", Encoding.UTF8.GetString([.. claims.Sequence]));
    }
}
