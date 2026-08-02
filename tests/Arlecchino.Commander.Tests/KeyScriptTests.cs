using System;
using Arlecchino.Commander.Frames;
using Xunit;

namespace Arlecchino.Commander.Tests;

/// <summary>
/// Keys written down as words, which is how a screenshot is scripted. A name read wrongly here shows up
/// as a picture of the wrong screen in the documentation, with nothing failing anywhere.
/// </summary>
public sealed class KeyScriptTests
{
    [Theory]
    [InlineData("enter", ConsoleKey.Enter)]
    [InlineData("Enter", ConsoleKey.Enter)]
    [InlineData("esc", ConsoleKey.Escape)]
    [InlineData("tab", ConsoleKey.Tab)]
    [InlineData("space", ConsoleKey.Spacebar)]
    [InlineData("up", ConsoleKey.UpArrow)]
    [InlineData("down", ConsoleKey.DownArrow)]
    [InlineData("left", ConsoleKey.LeftArrow)]
    [InlineData("right", ConsoleKey.RightArrow)]
    [InlineData("home", ConsoleKey.Home)]
    [InlineData("end", ConsoleKey.End)]
    [InlineData("pageup", ConsoleKey.PageUp)]
    [InlineData("pagedown", ConsoleKey.PageDown)]
    [InlineData("backspace", ConsoleKey.Backspace)]
    public void ANamedKeyIsTheKeyItNames(string piece, ConsoleKey expected)
    {
        Assert.Equal(expected, KeyScript.One(piece).Key);
    }

    [Theory]
    [InlineData("f1", ConsoleKey.F1)]
    [InlineData("F5", ConsoleKey.F5)]
    [InlineData("f12", ConsoleKey.F12)]
    public void TheFunctionKeysAreCountedFromOne(string piece, ConsoleKey expected)
    {
        Assert.Equal(expected, KeyScript.One(piece).Key);
    }

    [Theory]
    [InlineData("Ctrl+r", ConsoleModifiers.Control)]
    [InlineData("Alt+f1", ConsoleModifiers.Alt)]
    [InlineData("Shift+f6", ConsoleModifiers.Shift)]
    public void AModifierIsReadOffTheFront(string piece, ConsoleModifiers expected)
    {
        Assert.Equal(expected, KeyScript.One(piece).Modifiers);
    }

    [Fact]
    public void ModifiersStackUpRatherThanReplacingOneAnother()
    {
        var key = KeyScript.One("Ctrl+Shift+f6");

        Assert.Equal(ConsoleKey.F6, key.Key);
        Assert.Equal(ConsoleModifiers.Control | ConsoleModifiers.Shift, key.Modifiers);
    }

    [Fact]
    public void ModifiersAreReadWhateverTheyAreTypedIn()
    {
        Assert.Equal(ConsoleModifiers.Control, KeyScript.One("CTRL+r").Modifiers);
        Assert.Equal(ConsoleModifiers.Alt, KeyScript.One("alt+f1").Modifiers);
    }

    /// <summary>
    /// A screen that reads what was typed rather than which key it was needs the character too, and
    /// space is the one that is easy to hand over as a key with nothing in it.
    /// </summary>
    [Fact]
    public void ANamedKeyCarriesTheCharacterATerminalWouldSend()
    {
        Assert.Equal(' ', KeyScript.One("space").KeyChar);
        Assert.Equal('\r', KeyScript.One("enter").KeyChar);
        Assert.Equal('\t', KeyScript.One("tab").KeyChar);
        Assert.Equal('\e', KeyScript.One("esc").KeyChar);
    }

    [Fact]
    public void AnythingThatIsNotANameIsWhatWasTyped()
    {
        var key = KeyScript.One("a");

        Assert.Equal('a', key.KeyChar);
        Assert.Equal(default, key.Modifiers);
    }

    [Fact]
    public void AModifierStillCountsOnSomethingTyped()
    {
        var key = KeyScript.One("Ctrl+x");

        Assert.Equal('x', key.KeyChar);
        Assert.Equal(ConsoleModifiers.Control, key.Modifiers);
    }
}
