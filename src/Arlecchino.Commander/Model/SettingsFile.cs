using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Arlecchino.Commander.Model;

/// <summary>
/// Where what is kept between runs lives, and how it is written down.
///
/// One file of <c>name = "value"</c> lines under the folder the desktop convention puts a program's
/// settings in — <c>XDG_CONFIG_HOME</c> when the environment names one, and <c>~/.config</c> when it
/// does not. The same rule everywhere, Windows included: a file manager is run from a terminal, and a
/// terminal is where that convention is understood.
///
/// A name nothing here knows about is kept as it was found and written back out again. Settings are
/// added between one version and the next, and a file edited by hand should not lose what an older
/// build had no name for.
/// </summary>
public static class SettingsFile
{
    private const string Folder = "arlecchino.commander";
    private const string Name = "settings.toml";
    private const string Head = "# Arlecchino Commander · settings";

    /// <summary>The file itself, whether anything has been written to it yet or not.</summary>
    /// <returns>Its full path.</returns>
    public static string Place() => Path.Combine(Home(), Folder, Name);

    /// <summary>
    /// Reads what is in it. A file that is not there, or that cannot be read, is the same as one holding
    /// nothing: settings are what somebody asked for on top of the defaults, so there is nothing to
    /// report and nothing to fail.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <returns>Every name it names, and what it says each of them is.</returns>
    public static Dictionary<string, string> Read(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (!File.Exists(path))
            {
                return values;
            }

            foreach (var line in File.ReadAllLines(path))
            {
                Take(values, line);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return values;
        }

        return values;
    }

    /// <summary>
    /// Writes them all back, the folder made first if it was never there. Whether it worked is answered
    /// rather than thrown: a setting that could not be written is still in force for this run, and that
    /// is worth saying on the output row rather than taking the application down for.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <param name="values">What to put in it.</param>
    /// <returns><c>true</c> when it was written.</returns>
    public static bool Write(string path, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrEmpty(path);

        var written = new StringBuilder().AppendLine(Head).AppendLine();

        foreach (var (name, value) in values)
        {
            written.Append(name).Append(" = \"").Append(Escaped(value)).AppendLine("\"");
        }

        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(path, written.ToString());

            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>One line of the file. Comments and blanks are skipped; anything else names a setting.</summary>
    /// <param name="values">Everything read out of the file so far.</param>
    /// <param name="line">The line.</param>
    private static void Take(Dictionary<string, string> values, string line)
    {
        var text = line.Trim();

        if (text.Length == 0 || text[0] is '#' or '[')
        {
            return;
        }

        var split = text.IndexOf('=', StringComparison.Ordinal);

        if (split <= 0)
        {
            return;
        }

        var name = text[..split].Trim();

        if (name.Length > 0)
        {
            values[name] = Unquoted(text[(split + 1)..].Trim());
        }
    }

    /// <summary>What a value says with the surrounding quotes taken off, if it had any.</summary>
    /// <param name="value">As it was written.</param>
    /// <returns>What it means.</returns>
    private static string Unquoted(string value) =>
        value is ['"', _, ..] && value[^1] == '"'
            ? value[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal)
            : value;

    /// <summary>A value with what would end the quoted string in it made safe to write.</summary>
    /// <param name="value">What it is.</param>
    /// <returns>What to write between the quotes.</returns>
    private static string Escaped(string value) => value.Replace("\"", "\\\"", StringComparison.Ordinal);

    /// <summary>The folder a program's settings belong under on this machine.</summary>
    /// <returns>The folder, which may not exist yet.</returns>
    private static string Home() =>
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } named
            ? named
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
}
