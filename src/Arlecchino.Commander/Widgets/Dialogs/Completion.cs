using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Widgets.Dialogs;

/// <summary>
/// Finishing a half-typed path from what is really on the far side. The key press never waits on the
/// listing: the field fills in when the answer arrives, and only if nothing was typed meanwhile.
/// </summary>
public static class Completion
{
    /// <summary>Finishes the path in the field of an operation being asked.</summary>
    /// <param name="asking">What is being asked.</param>
    public static void Finish(Operation asking)
    {
        if (asking.Target is not { } source)
        {
            return;
        }

        var text = asking.Value;
        var cut = text.LastIndexOfAny(['/', '\\']);
        var folder = cut < 0 ? text : cut == 0 ? text[..1] : text[..cut];
        var start = cut < 0 ? "" : text[(cut + 1)..];

        Answers.From(
            () => Names(source, folder, start),
            match =>
            {
                if (match.Length == 0 || asking.Value != text)
                {
                    return;
                }

                asking.Value = source.Combine(folder, match);
            });
    }

    /// <summary>
    /// The one name a half-typed one can only mean, or as much of it as every candidate agrees on.
    /// Completing to the longest shared beginning is what a shell does.
    /// </summary>
    /// <param name="source">Where to look.</param>
    /// <param name="folder">The folder that was typed out in full.</param>
    /// <param name="start">What was typed of the name.</param>
    /// <returns>The name to put there, or nothing when nothing fits.</returns>
    private static async Task<string> Names(IFileSource source, string folder, string start)
    {
        try
        {
            var entries = await source.ListAsync(folder, showHidden: true, CancellationToken.None)
                .ConfigureAwait(false);
            var stem = "";

            foreach (var entry in entries)
            {
                if (entry.IsParent ||
                    !entry.IsFolder ||
                    !entry.Name.StartsWith(start, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                stem = stem.Length == 0 ? entry.Name : Common(stem, entry.Name);
            }

            return stem;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                                          InvalidOperationException)
        {
            return "";
        }
    }

    /// <summary>How much two names agree on from the front.</summary>
    /// <param name="first">One name.</param>
    /// <param name="second">The other.</param>
    /// <returns>The beginning they share.</returns>
    private static string Common(string first, string second)
    {
        var stem = 0;

        while (stem < first.Length &&
               stem < second.Length &&
               char.ToLowerInvariant(first[stem]) == char.ToLowerInvariant(second[stem]))
        {
            stem++;
        }

        return first[..stem];
    }
}
