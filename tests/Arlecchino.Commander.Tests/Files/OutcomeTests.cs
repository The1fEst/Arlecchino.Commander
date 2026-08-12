using System.Threading.Tasks;
using Arlecchino.Commander.Files.Work;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// What a long job says about itself while it runs and once it is over. What the bar and the error list
/// say has to hold while several threads report into them at once.
/// </summary>
public sealed class OutcomeTests
{
    [Fact]
    public void BeforeItIsSizedUpThereIsNothingToDraw()
    {
        var outcome = new Outcome();

        Assert.False(outcome.IsMeasured);
        Assert.Equal(0, outcome.Share);
    }

    [Fact]
    public void BytesCarryHowFarAlongItIsWhenBytesAreMoving()
    {
        var outcome = new Outcome();

        outcome.Planning(new(2, 0, 100));
        outcome.Counted(25);

        Assert.True(outcome.IsMeasured);
        Assert.Equal(0.25, outcome.Share, 3);
    }

    /// <summary>
    /// A delete shifts no bytes at all, and the bar still has to fill as it works through the tree.
    /// </summary>
    [Fact]
    public void CountsCarryItWhenNoBytesMove()
    {
        var outcome = new Outcome();

        outcome.Planning(new(0, 4, 0));
        outcome.CountedFolder();
        outcome.CountedFolder();

        Assert.Equal(0.5, outcome.Share, 3);
    }

    [Fact]
    public void ItNeverClaimsMoreThanFinished()
    {
        var outcome = new Outcome();

        outcome.Planning(new(1, 0, 10));
        outcome.Counted(999);

        Assert.Equal(1, outcome.Share);
    }

    [Fact]
    public void AFailureIsRememberedWithWhatItWasAbout()
    {
        var outcome = new Outcome();

        Assert.False(outcome.Failed);

        outcome.Failing("notes.txt", "permission denied");

        Assert.True(outcome.Failed);
        Assert.Equal(["notes.txt: permission denied"], outcome.Errors);
    }

    [Fact]
    public void OneOutcomeTakesAnotherOneIn()
    {
        var whole = new Outcome();
        var part = new Outcome();

        part.Counted(40);
        part.CountedFolder();
        part.Failing("beta.txt", "gone");

        whole.Planning(new(1, 1, 40));
        whole.Absorb(part);

        Assert.True(whole.Failed);
        Assert.Equal(["beta.txt: gone"], whole.Errors);
        Assert.Equal(1, whole.Share);
    }

    [Fact]
    public void WhatItIsWorkingOnIsSaidOnceItIsSizedUp()
    {
        var outcome = new Outcome();

        outcome.Planning(new(2, 0, 20));
        outcome.Reached("alpha.txt");
        outcome.Counted(10);

        Assert.Contains("alpha.txt", outcome.Progress(), System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Several threads report into one of these at once — a copy walks a tree in parallel — so the
    /// counts have to add up rather than lose some along the way.
    /// </summary>
    [Fact]
    public async Task ItAddsUpWhenSeveralThreadsReportAtOnce()
    {
        var outcome = new Outcome();

        outcome.Planning(new(1000, 0, 1000));

        await Task.WhenAll(Reporting(), Reporting(), Reporting(), Reporting());

        Assert.Equal(1, outcome.Share);
        Assert.False(outcome.Failed);

        Task Reporting() => Task.Run(() =>
        {
            for (var step = 0; step < 250; step++)
            {
                outcome.Counted(1);
            }
        });
    }
}
