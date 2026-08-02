using System;
using System.IO;
using System.Linq;
using Arlecchino.Commander.Files;
using Arlecchino.Commander.Views;
using Xunit;

namespace Arlecchino.Commander.Tests;

/// <summary>
/// The search, walking a folder of its own. It runs off the drawing thread and reports back on it, so
/// waiting for it here means drawing frames rather than sleeping.
/// </summary>
public sealed class FinderTests : IDisposable
{
    private readonly ScreenApp _app = new(ViewKind.Find);
    private readonly LocalSource _source = new();

    public FinderTests()
    {
        var under = Directory.CreateDirectory(Path.Combine(_app.Folder, "under")).FullName;

        _app.Write("alpha.txt", "the needle is here");
        _app.Write("beta.md", "nothing of interest");
        File.WriteAllText(Path.Combine(under, "gamma.txt"), "nothing of interest");
    }

    public void Dispose() => _app.Dispose();

    private bool Search(string pattern, string content = "")
    {
        var done = false;

        _app.Finder.Start(_source, _app.Folder, pattern, content, () => done = true);

        return _app.Until(() => done);
    }

    [Fact]
    public void APatternFindsWhatMatchesItAnywhereUnderTheFolder()
    {
        Assert.True(Search("*.txt"));

        var found = _app.Finder.Found.Value.Select(static hit => hit.Entry.Name).ToList();

        Assert.Contains("alpha.txt", found);
        Assert.Contains("gamma.txt", found);
        Assert.DoesNotContain("beta.md", found);
    }

    [Fact]
    public void WhatItLookedThroughIsCounted()
    {
        Assert.True(Search("*.txt"));

        Assert.True(_app.Finder.Looked >= 2, $"looked through {_app.Finder.Looked} folders");
        Assert.Equal(_app.Folder, _app.Finder.Root);
    }

    [Fact]
    public void AskingForTextNarrowsItToTheFilesHoldingThat()
    {
        Assert.True(Search("*.txt", "needle"));

        var found = _app.Finder.Found.Value.Select(static hit => hit.Entry.Name).ToList();

        Assert.Equal(["alpha.txt"], found);
    }

    [Fact]
    public void WhatIsBeingLookedForIsSaidSoItCanBeShown()
    {
        Assert.True(Search("*.txt", "needle"));

        Assert.Contains("*.txt", _app.Finder.What, StringComparison.Ordinal);
        Assert.Contains("needle", _app.Finder.What, StringComparison.Ordinal);
    }

    [Fact]
    public void FindingNothingIsFinishingRatherThanHanging()
    {
        Assert.True(Search("*.nothing-is-called-this"));

        Assert.Empty(_app.Finder.Found.Value);
        Assert.False(_app.Finder.IsRunning);
    }

    [Fact]
    public void WhatItFoundReachesTheScreen()
    {
        Assert.True(Search("*.txt"));

        Assert.Contains("alpha.txt", _app.Frame(), StringComparison.Ordinal);
    }
}
