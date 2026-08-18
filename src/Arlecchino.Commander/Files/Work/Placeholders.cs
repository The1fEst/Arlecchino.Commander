using System;
using System.Collections.Generic;
using System.Text;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Files.Work;

/// <summary>
/// What a command line means by <c>%s</c> and the rest of them, so a typed command can act on what the panel
/// has marked. Every path goes in escaped by the end that will read it.
/// </summary>
public static class Placeholders
{
    /// <summary>
    /// Fills in what the panel knows, using the words Midnight Commander uses. Anything else after a percent
    /// is left alone, percent and all, since a command line is full of percents meant for something else.
    /// <list type="bullet">
    ///   <item><description><c>%f</c> — the file under the cursor.</description></item>
    ///   <item><description><c>%s</c> — everything marked, or the file under the cursor when nothing is.</description></item>
    ///   <item><description><c>%d</c> — the folder the panel is looking at.</description></item>
    ///   <item><description><c>%%</c> — a percent sign, for a command that wanted one.</description></item>
    /// </list>
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
        if (!command.Contains('%', StringComparison.Ordinal))
        {
            return command;
        }

        var builder = new StringBuilder(command.Length);

        for (var at = 0; at < command.Length; at++)
        {
            if (command[at] != '%' || at + 1 == command.Length)
            {
                builder.Append(command[at]);

                continue;
            }

            switch (command[at + 1])
            {
                case '%':
                    builder.Append('%');
                    at++;

                    break;
                case 'f':
                    builder.Append(current is null ? "" : source.Quote(current.Path));
                    at++;

                    break;
                case 'd':
                    builder.Append(source.Quote(folder));
                    at++;

                    break;
                case 's':
                    Spread(builder, source, targets);
                    at++;

                    break;
                default:
                    builder.Append(command[at]);

                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>Writes every path out, escaped and a space apart.</summary>
    /// <param name="builder">What is being built.</param>
    /// <param name="source">The end that will read them.</param>
    /// <param name="targets">The paths.</param>
    private static void Spread(StringBuilder builder, IFileSource source, IReadOnlyList<FileEntry> targets)
    {
        for (var at = 0; at < targets.Count; at++)
        {
            if (at > 0)
            {
                builder.Append(' ');
            }

            builder.Append(source.Quote(targets[at].Path));
        }
    }
}
