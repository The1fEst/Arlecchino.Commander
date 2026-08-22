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
    /// <param name="blanks">Whether the terminal it came from paints itself blank to begin with.</param>
    /// <returns>The letters kept, and whether the screen was claimed along the way.</returns>
    private static (string Letters, bool Claimed) Read(string text, bool blanks = false)
    {
        var claims = new Claims(blanks);
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
    /// A terminal the machine makes starts by saying its screen is blank and its cursor at the top of
    /// it. Read as a claim, that would hand the screen to every command on such a machine.
    /// </summary>
    /// <param name="sequence">What came off a terminal that opens blank, before the command wrote a word.</param>
    [Theory]
    [InlineData("\e[2J")]
    [InlineData("\e[H")]
    [InlineData("\e[?25l\e[2J\e[m\e[H")]
    public void TheBlankScreenATerminalOpensWithIsNotAClaim(string sequence) =>
        Assert.False(Read(sequence, blanks: true).Claimed);

    /// <summary>
    /// The same instructions once the command has written something are the command's own, since the
    /// terminal has had its say. A program that draws over the screen it was given is caught here.
    /// </summary>
    /// <param name="sequence">What the command printed after a line of its own.</param>
    [Theory]
    [InlineData("before\e[2J")]
    [InlineData("before\e[6;1H")]
    [InlineData("\e[?25l\e[2J\e[m\e[Hbefore\e[H")]
    public void DrawingAfterTheTerminalHasOpenedIsAClaim(string sequence) =>
        Assert.True(Read(sequence, blanks: true).Claimed);

    /// <summary>
    /// Only the two a terminal would do itself are excused. A program that swaps screens or asks for the
    /// mouse before it has printed a word is claiming the screen wherever it runs.
    /// </summary>
    /// <param name="sequence">What the command printed straight after the terminal opened.</param>
    [Theory]
    [InlineData("\e[?25l\e[2J\e[m\e[H\e[?1049h")]
    [InlineData("\e[?1000h")]
    public void AskingForTheScreenWhileTheTerminalOpensIsStillAClaim(string sequence) =>
        Assert.True(Read(sequence, blanks: true).Claimed);

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
        var claims = new Claims(blanks: false);
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
        var claims = new Claims(blanks: false);

        foreach (var letter in "\e[?1049h"u8)
        {
            claims.Takes(letter);
        }

        Assert.Equal("\e[?1049h", Encoding.UTF8.GetString([.. claims.Sequence]));
    }
}
