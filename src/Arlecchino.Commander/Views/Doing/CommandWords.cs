using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Editing;

namespace Arlecchino.Commander.Views.Doing;

/// <summary>
/// What a half-typed word on the command line could still turn into. The first word of a command is a
/// program, and every word after it is something in the folder the panel is looking at.
/// </summary>
public sealed class CommandWords : ISuggestsWords
{
    private static readonly char[] Separators = ['/', '\\'];

    private readonly Pair _panels;
    private readonly IReadOnlyList<string> _history;

    /// <summary>Answers for a pair of panels.</summary>
    /// <param name="panels">The two panels, of which the active one says where names are looked for.</param>
    /// <param name="history">Commands run before, which are programs that have already proved to be here.</param>
    public CommandWords(Pair panels, IReadOnlyList<string> history)
    {
        _panels = panels;
        _history = history;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<string>> SuggestAsync(CompletionAsk ask, CancellationToken token)
    {
        var word = Bare(ask.Word);
        var before = ask.Before.Trim();

        if (before.Length == 0 && word.IndexOfAny(Separators) < 0 && !word.StartsWith('~'))
        {
            return Ran(word, await Task.Run(() => Programs.Starting(word), token).ConfigureAwait(false));
        }

        return await Named(word, before == "cd", token).ConfigureAwait(false);
    }

    /// <summary>
    /// The programs on this machine, with the ones already run put in front of them. What was typed once is
    /// likelier to be typed again than anything the path happens to hold under the same letters.
    /// </summary>
    /// <param name="word">What was typed of the name.</param>
    /// <param name="onPath">What the path holds under it.</param>
    /// <returns>The names to offer, without repeats.</returns>
    private List<string> Ran(string word, IReadOnlyList<string> onPath)
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var command in _history)
        {
            var space = command.IndexOf(' ', StringComparison.Ordinal);
            var name = space < 0 ? command : command[..space];

            if (name.StartsWith(word, StringComparison.OrdinalIgnoreCase) && seen.Add(name))
            {
                found.Add(name);
            }
        }

        foreach (var name in onPath)
        {
            if (seen.Add(name))
            {
                found.Add(name);
            }
        }

        return found;
    }

    /// <summary>
    /// What is in the folder the half-typed path points at, narrowed to the names that begin the way it
    /// ends. A folder is offered with the separator after it, so the next press carries on inside it.
    /// </summary>
    /// <param name="word">The path as typed, with its quoting taken off.</param>
    /// <param name="foldersOnly">Whether files are no answer, as they are not to a <c>cd</c>.</param>
    /// <param name="token">Gives up the wait.</param>
    /// <returns>The paths to offer, each ready to stand on the line as it is.</returns>
    private async Task<IReadOnlyList<string>> Named(string word, bool foldersOnly, CancellationToken token)
    {
        var panel = _panels.Active;
        var source = panel.Source;
        var cut = word.LastIndexOfAny(Separators);
        var typed = cut < 0 ? "" : word[..(cut + 1)];
        var start = cut < 0 ? word : word[(cut + 1)..];
        var separator = source.Combine("a", "b").Contains('\\', StringComparison.Ordinal) ? '\\' : '/';
        var found = new List<string>();

        try
        {
            var entries = await source
                .ListAsync(Where(source, panel.Folder, typed), start.StartsWith('.'), token)
                .ConfigureAwait(false);

            foreach (var entry in entries)
            {
                if (entry.IsParent ||
                    (foldersOnly && !entry.IsFolder) ||
                    !entry.Name.StartsWith(start, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                found.Add(Quoted(typed + entry.Name + (entry.IsFolder ? separator : "")));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                                          InvalidOperationException)
        {
            return [];
        }

        found.Sort(NaturalSort.Compare);

        return found;
    }

    /// <summary>
    /// Which folder a half-typed path is looking in. A path of its own is taken as it stands, and a tilde
    /// is the home of whatever source it is.
    /// </summary>
    /// <param name="source">Where the names come from.</param>
    /// <param name="folder">Where the panel is looking.</param>
    /// <param name="typed">What was typed of the folder, with the separator still on the end.</param>
    /// <returns>The folder to list.</returns>
    private static string Where(IFileSource source, string folder, string typed)
    {
        if (typed.Length == 0)
        {
            return folder;
        }

        var trimmed = typed.Length > 1 ? typed.TrimEnd(Separators) : typed;

        if (trimmed.StartsWith('~'))
        {
            return trimmed.Length == 1 ? source.Home : source.Combine(source.Home, trimmed[2..]);
        }

        return trimmed[0] == '/' || trimmed[0] == '\\' || (trimmed.Length > 1 && trimmed[1] == ':')
            ? trimmed
            : source.Combine(folder, trimmed);
    }

    /// <summary>
    /// A path with the quoting a command line needs to read it as one word, which is the quoting the panel
    /// keys already put around a name they insert. A folder is left open, since more of the path follows.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The path as it should stand on the line.</returns>
    private static string Quoted(string path)
    {
        if (!path.Contains(' ', StringComparison.Ordinal))
        {
            return path;
        }

        return path.EndsWith('/') || path.EndsWith('\\') ? $"\"{path}" : $"\"{path}\"";
    }

    /// <summary>The word without whatever quoting was typed around it.</summary>
    /// <param name="word">The word as it stands on the line.</param>
    /// <returns>The path it means.</returns>
    private static string Bare(string word) => word.Replace("\"", "", StringComparison.Ordinal);
}
