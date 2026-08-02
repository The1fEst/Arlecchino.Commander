using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Files;

/// <summary>One entry of the user menu: what it is called and what it runs.</summary>
/// <param name="Title">The line shown in the menu.</param>
/// <param name="Commands">The commands under it, in order.</param>
public sealed record MenuEntry(string Title, IReadOnlyList<string> Commands);

/// <summary>
/// The menu behind <c>F2</c>, kept in a file the way Midnight Commander keeps it: a line that starts
/// in the first column names an entry, and the indented lines under it are what that entry runs. A
/// command may stand in for what the panels are pointing at — <c>%f</c> the file, <c>%d</c> the
/// folder, <c>%t</c> everything marked, and the capitals for the other panel.
/// </summary>
public static class UserMenu
{
    private const string Starter = """
                                   # The menu F2 opens. A line in the first column names an entry;
                                   # the indented lines under it are run in order, where the panel
                                   # is looking. %f is the file under the cursor, %t everything
                                   # marked, %d this folder, %F and %D the same on the other panel.

                                   Count the lines of the marked files
                                       wc -l %t

                                   Compress the file under the cursor
                                       tar czf %f.tar.gz %f

                                   Copy the folder listing to the other panel
                                       ls -la > %D/listing.txt

                                   """;

    public static string Location => Path.Combine(Listing.Home(), ".config", "arlecchino-commander", "menu");

    /// <summary>Reads the menu.</summary>
    /// <returns>The entries, or none when the file is not there or holds nothing.</returns>
    public static IReadOnlyList<MenuEntry> Read()
    {
        try
        {
            return File.Exists(Location) ? Parse(File.ReadAllLines(Location)) : [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Writes a menu to start from, so the first <c>F2</c> has something to show.</summary>
    /// <returns><c>true</c> when it was written.</returns>
    public static bool WriteStarter()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Location)!);
            File.WriteAllText(Location, Starter);

            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Puts what the panels are pointing at into a command.</summary>
    /// <param name="command">The command as it is written in the file.</param>
    /// <param name="file">The file under the cursor.</param>
    /// <param name="marked">Everything marked, already quoted and spaced.</param>
    /// <param name="folder">The folder this panel is showing.</param>
    /// <param name="other">The folder the other panel is showing.</param>
    /// <param name="otherFile">The file under the other panel's cursor.</param>
    /// <returns>The command to run.</returns>
    public static string Fill(string command, string file, string marked, string folder, string other,
        string otherFile)
    {
        ArgumentNullException.ThrowIfNull(command);

        return new StringBuilder(command)
            .Replace("%t", marked)
            .Replace("%f", Quoted(file))
            .Replace("%d", Quoted(folder))
            .Replace("%F", Quoted(otherFile))
            .Replace("%D", Quoted(other))
            .ToString();
    }

    /// <summary>
    /// The whole of an entry as one command. The lines are joined rather than run one at a time so
    /// that a failure stops the rest — an entry that unpacks an archive and then deletes it must not
    /// get as far as the deleting when the unpacking did not work.
    /// </summary>
    /// <param name="entry">The entry that was chosen.</param>
    /// <param name="file">The file under the cursor.</param>
    /// <param name="marked">Everything marked, already quoted and spaced.</param>
    /// <param name="folder">The folder this panel is showing.</param>
    /// <param name="other">The folder the other panel is showing.</param>
    /// <param name="otherFile">The file under the other panel's cursor.</param>
    /// <returns>The command to run.</returns>
    public static string Whole(MenuEntry entry, string file, string marked, string folder, string other,
        string otherFile)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var whole = new StringBuilder();

        foreach (var command in entry.Commands)
        {
            whole
                .Append(whole.Length == 0 ? "" : " && ")
                .Append(Fill(command, file, marked, folder, other, otherFile));
        }

        return whole.ToString();
    }

    public static string Quoted(string piece) =>
        piece.Contains(' ', StringComparison.Ordinal) ? $"\"{piece}\"" : piece;

    private static List<MenuEntry> Parse(IReadOnlyList<string> lines)
    {
        var entries = new List<MenuEntry>();
        var commands = new List<string>();
        var title = "";

        foreach (var line in lines)
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (char.IsWhiteSpace(line[0]))
            {
                if (title.Length > 0)
                {
                    commands.Add(line.Trim());
                }

                continue;
            }

            Close(entries, title, commands);

            title = line.Trim();
            commands = [];
        }

        Close(entries, title, commands);

        return entries;
    }

    private static void Close(List<MenuEntry> entries, string title, List<string> commands)
    {
        if (title.Length > 0 && commands.Count > 0)
        {
            entries.Add(new(title, commands));
        }
    }
}
