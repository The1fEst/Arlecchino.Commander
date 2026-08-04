using System;
using System.Collections.Generic;
using System.Text;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Files.Work;

/// <summary>
/// What a command line means by <c>%s</c> and the rest of them. A file manager whose command line
/// cannot name what the panel is showing is a worse terminal than the terminal; these are the words
/// that let a typed command act on what was marked.
///
/// Every path goes in escaped by the end that will read it, so a name with a space or an apostrophe in
/// it arrives as one word rather than as several or as a syntax error. That is the whole reason this
/// exists rather than being a few calls to <c>Replace</c>: a marked file called <c>don't go.txt</c>
/// pasted in raw would end a quoted string somebody else opened.
/// </summary>
public static class Placeholders
{
    /// <summary>
    /// Fills in what the panel knows. The words are those of Midnight Commander, which is where anybody
    /// reaching for them learned them:
    /// <list type="bullet">
    ///   <item><description><c>%f</c> — the file under the cursor.</description></item>
    ///   <item><description><c>%s</c> — everything marked, or the file under the cursor when nothing is.</description></item>
    ///   <item><description><c>%d</c> — the folder the panel is looking at.</description></item>
    ///   <item><description><c>%%</c> — a percent sign, for a command that wanted one.</description></item>
    /// </list>
    /// Anything else after a percent is left alone, percent and all, because a command line is full of
    /// percents that were never meant for us — a Windows variable, a <c>printf</c> format, a URL.
    /// </summary>
    /// <param name="command">What was typed.</param>
    /// <param name="source">The end the command will run on, which decides how a path is escaped.</param>
    /// <param name="folder">The folder the panel is looking at.</param>
    /// <param name="targets">What is marked, or the one under the cursor.</param>
    /// <param name="current">What the cursor is on, which may be nothing.</param>
    /// <returns>The command with the words filled in.</returns>
    public static string Expand(
        string command,
        IFileSource source,
        string folder,
        IReadOnlyList<FileEntry> targets,
        FileEntry? current)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(targets);

        if (!command.Contains('%', StringComparison.Ordinal))
        {
            return command;
        }

        var built = new StringBuilder(command.Length);

        for (var at = 0; at < command.Length; at++)
        {
            if (command[at] != '%' || at + 1 == command.Length)
            {
                built.Append(command[at]);

                continue;
            }

            switch (command[at + 1])
            {
                case '%':
                    built.Append('%');
                    at++;

                    break;
                case 'f':
                    built.Append(current is null ? "" : source.Quote(current.Path));
                    at++;

                    break;
                case 'd':
                    built.Append(source.Quote(folder));
                    at++;

                    break;
                case 's':
                    Spread(built, source, targets);
                    at++;

                    break;
                default:
                    built.Append(command[at]);

                    break;
            }
        }

        return built.ToString();
    }

    /// <summary>Writes every path out, escaped and a space apart.</summary>
    /// <param name="built">What is being built.</param>
    /// <param name="source">The end that will read them.</param>
    /// <param name="targets">The paths.</param>
    private static void Spread(StringBuilder built, IFileSource source, IReadOnlyList<FileEntry> targets)
    {
        for (var at = 0; at < targets.Count; at++)
        {
            if (at > 0)
            {
                built.Append(' ');
            }

            built.Append(source.Quote(targets[at].Path));
        }
    }
}
