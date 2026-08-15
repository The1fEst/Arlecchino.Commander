using System.Linq;
using Arlecchino.Commander.Widgets.Chrome;
using Xunit;

namespace Arlecchino.Commander.Tests.Widgets;

/// <summary>
///     Breaking a line being typed into the rows it is shown on. What matters as much as where it breaks is
///     that nothing is dropped at the break, since the caret is counted in those very characters.
/// </summary>
public sealed class CommandLineWrapTests
{
    [Fact]
    public void WhatFitsStaysOnOneRow()
    {
        CommandLineRow[] wanted = [new(0, "git status")];

        Assert.Equal(wanted, CommandLineWrap.Rows("git status", 20));
    }

    [Fact]
    public void ItBreaksAtTheLastSpaceThatFits()
    {
        CommandLineRow[] wanted = [new(0, "git "), new(4, "commit "), new(11, "-m hello")];

        Assert.Equal(wanted, CommandLineWrap.Rows("git commit -m hello", 8));
    }

    [Fact]
    public void AWordWiderThanTheRowIsBrokenWhereItRunsOut()
    {
        CommandLineRow[] wanted = [new(0, "aaaaa"), new(5, "aaaaa"), new(10, "aa")];

        Assert.Equal(wanted, CommandLineWrap.Rows("aaaaaaaaaaaa", 5));
    }

    /// <summary>
    ///     The rows are what the text is, cut up. A wrap that trims the spaces it broke at reads the same but
    ///     cannot be typed on: the caret would stand one character to the left for every space swallowed.
    /// </summary>
    [Fact]
    public void TheRowsTogetherSpellWhatWasTyped()
    {
        const string typed = "grep -rn \"the words being looked for\" src --include \"*.cs\"";

        Assert.Equal(typed, string.Concat(CommandLineWrap.Rows(typed, 13).Select(row => row.Text)));
    }

    [Fact]
    public void AWideSymbolIsCountedInTheColumnsItTakes()
    {
        CommandLineRow[] wanted = [new(0, "世界"), new(2, "世界")];

        Assert.Equal(wanted, CommandLineWrap.Rows("世界世界", 4));
    }

    /// <summary>
    ///     What the caret is for: every place the cursor can be in has a row and a column, and the two point
    ///     back at the character the cursor is counting to.
    /// </summary>
    [Fact]
    public void TheCaretLandsOnTheCharacterTheCursorCountsTo()
    {
        const string typed = "git commit -m hello";

        var rows = CommandLineWrap.Rows(typed, 8);

        for (var cursor = 0; cursor <= typed.Length; cursor++)
        {
            var (row, column) = CommandLineWrap.Caret(rows, cursor);

            Assert.Equal(cursor, rows[row].Start + column);
        }
    }

    [Fact]
    public void TheCaretIsOnTheRowTheCharacterWasCarriedOnto()
    {
        var rows = CommandLineWrap.Rows("git commit -m hello", 8);

        Assert.Equal((2, 4), CommandLineWrap.Caret(rows, 15));
    }

    [Fact]
    public void TheCaretStandsAfterTheLastCharacterTyped()
    {
        var rows = CommandLineWrap.Rows("git commit -m hello", 8);

        Assert.Equal((2, 8), CommandLineWrap.Caret(rows, 19));
    }

    [Fact]
    public void TheCaretIsCountedInColumnsRatherThanCharacters()
    {
        var rows = CommandLineWrap.Rows("世界世界", 4);

        Assert.Equal((1, 2), CommandLineWrap.Caret(rows, 3));
    }
}
