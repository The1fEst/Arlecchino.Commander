using System;
using System.Collections.Generic;
using System.Linq;
using Arlecchino.Commander.Model;
using Xunit;

namespace Arlecchino.Commander.Tests.Model;

/// <summary>
/// Sorting names the way a person reads them. What is asserted here is mostly the order of whole lists
/// rather than single pairs, because the order is the thing anybody actually sees.
/// </summary>
public sealed class NaturalSortTests
{
    private static IReadOnlyList<string> Sorted(params string[] names)
    {
        var sorted = names.ToList();
        sorted.Sort(NaturalSort.Compare);

        return sorted;
    }

    [Fact]
    public void TenComesAfterTwo()
    {
        Assert.Equal(
            ["file2.txt", "file9.txt", "file10.txt", "file100.txt"],
            Sorted("file10.txt", "file100.txt", "file2.txt", "file9.txt"));
    }

    [Fact]
    public void NumbersAreReadThroughTheWholeName()
    {
        Assert.Equal(
            ["chapter 1 part 2", "chapter 1 part 10", "chapter 2 part 1"],
            Sorted("chapter 2 part 1", "chapter 1 part 10", "chapter 1 part 2"));
    }

    /// <summary>
    /// Padding is deliberate when somebody types it, so the padded names keep the order the padding was
    /// for, and the shorter padding sorts first.
    /// </summary>
    [Fact]
    public void PaddedNumbersKeepTheirPadding()
    {
        Assert.Equal(["007", "08", "9"], Sorted("9", "08", "007"));
    }

    [Fact]
    public void CaseDoesNotDecideTheOrder()
    {
        Assert.Equal(["Apple", "banana", "Cherry"], Sorted("banana", "Cherry", "Apple"));
    }

    [Fact]
    public void AShorterNameComesFirstWhenItIsAPrefix()
    {
        Assert.Equal(["file", "file1", "file2"], Sorted("file2", "file", "file1"));
    }

    [Fact]
    public void ANameEqualsItself()
    {
        Assert.Equal(0, NaturalSort.Compare("report 12.txt", "report 12.txt"));
    }

    /// <summary>
    /// The comparison has to be a consistent order, or a sort built on it can throw rather than merely
    /// produce something odd. Every pair is checked against its opposite.
    /// </summary>
    [Fact]
    public void TheOrderIsConsistentBothWays()
    {
        string[] names =
            ["a", "A", "a1", "a01", "a2", "a10", "1", "01", "10", "", "2x", "2x1", "x", "10x", "9x"];

        foreach (var one in names)
        {
            foreach (var other in names)
            {
                Assert.Equal(Math.Sign(NaturalSort.Compare(one, other)), -Math.Sign(NaturalSort.Compare(other, one)));
            }
        }
    }

    [Fact]
    public void AnEmptyNameComesFirst()
    {
        Assert.Equal(["", "a"], Sorted("a", ""));
    }
}
