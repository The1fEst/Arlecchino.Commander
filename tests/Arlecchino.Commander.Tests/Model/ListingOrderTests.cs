using System.Collections.Generic;
using System.Linq;
using Arlecchino.Commander.Model;
using Xunit;

namespace Arlecchino.Commander.Tests.Model;

/// <summary>
/// The order rows come in when the tag column is the one sorted by. Folders keep the top of the panel
/// whatever the sorting is, since that is what the rank does.
/// </summary>
public sealed class ListingOrderTests
{
    private static FileEntry File(string name) =>
        new(name, $"/tmp/{name}", false, false, 0, default, false, false, false);

    private static IReadOnlyList<string> ByKind(params string[] names)
    {
        var rows = names.Select(File).ToList();
        rows.Sort((first, second) => Listing.Compare(first, second, Sorting.Kind, descending: false));

        return [.. rows.Select(row => row.Name)];
    }

    [Fact]
    public void TagsComeInTheirOwnOrderAndNamesSettleTheTies()
    {
        Assert.Equal(
            ["one.yml", "two.json", "a.png", "z.png", "b.md"],
            ByKind("z.png", "b.md", "two.json", "a.png", "one.yml"));
    }

    [Fact]
    public void WhatCarriesNoTagGoesLast()
    {
        Assert.Equal(["notes.md", "LICENSE"], ByKind("LICENSE", "notes.md"));
    }

    [Fact]
    public void FoldersStayOnTop()
    {
        var folder = new FileEntry("src", "/tmp/src", true, false, 0, default, false, false, false);
        var rows = new List<FileEntry> { File("a.md"), folder };

        rows.Sort((first, second) => Listing.Compare(first, second, Sorting.Kind, descending: false));

        Assert.Equal("src", rows[0].Name);
    }
}
