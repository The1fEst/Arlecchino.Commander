using System;
using System.Collections.Generic;
using System.IO;
using Arlecchino.Commander.Model;
using Xunit;

namespace Arlecchino.Commander.Tests.Model;

/// <summary>
///     The file settings are kept in: what it makes of what is written there, and what survives being
///     written and read back.
/// </summary>
public sealed class SettingsFileTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("commander-settings").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    /// <summary>A file that was never written is not an error. Nothing has been set, and that is all.</summary>
    [Fact]
    public void AFileThatIsNotThereHoldsNothing()
    {
        Assert.Empty(SettingsFile.Read(Path.Combine(_folder, "settings.toml")));
    }

    /// <summary>Comments, blank lines and a heading are skipped; a name and a value make a setting.</summary>
    [Fact]
    public void CommentsAndHeadingsAreSkipped()
    {
        var text = SettingsFile.Read(Written("# what this is\n[settings]\n\neditor = \"nvim\"\n"));

        Assert.Equal("nvim", Assert.Contains("editor", text));
    }

    /// <summary>A value written without quotes means what it says, since a file is edited by hand too.</summary>
    [Fact]
    public void AValueNeedsNoQuotes()
    {
        Assert.Equal("nano", Assert.Contains("editor", SettingsFile.Read(Written("editor = nano\n"))));
    }

    /// <summary>
    ///     A name this build has never heard of is written back out again. Settings are added between one
    ///     version and the next, and going back to an older one should not silently drop what the newer one
    ///     was keeping.
    /// </summary>
    [Fact]
    public void WhatIsWrittenComesBackAsItWent()
    {
        var path = Path.Combine(_folder, "kept", "settings.toml");
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["editor"] = "code --wait",
            ["something"] = "a \"quoted\" thing"
        };

        Assert.True(SettingsFile.Write(path, values));

        var text = SettingsFile.Read(path);

        Assert.Equal("code --wait", Assert.Contains("editor", text));
        Assert.Equal("a \"quoted\" thing", Assert.Contains("something", text));
    }

    /// <summary>The folder is made when it was never there, since nothing else is going to make it.</summary>
    [Fact]
    public void TheFolderIsMadeIfItIsMissing()
    {
        var path = Path.Combine(_folder, "one", "two", "settings.toml");

        Assert.True(SettingsFile.Write(path, new Dictionary<string, string> { ["editor"] = "vi" }));
        Assert.True(File.Exists(path));
    }

    /// <summary>A file with the text in it, for the tests that read one.</summary>
    /// <param name="text">What to put in it.</param>
    /// <returns>Its path.</returns>
    private string Written(string text)
    {
        var path = Path.Combine(_folder, "settings.toml");

        File.WriteAllText(path, text);

        return path;
    }
}
