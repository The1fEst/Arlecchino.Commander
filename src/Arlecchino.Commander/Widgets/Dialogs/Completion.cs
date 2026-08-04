using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Widgets.Dialogs;

/// <summary>
/// Finishing a half-typed path from what is really on the far side. The listing is a round trip on a
/// server, so the key press never waits on it — the field fills in when the answer arrives, and only
/// if nothing has been typed in the meantime.
/// </summary>
public static class Completion
{
    /// <summary>Finishes the path in the field of an operation being asked.</summary>
    /// <param name="asking">What is being asked.</param>
    public static void Finish(Operation asking)
    {
        ArgumentNullException.ThrowIfNull(asking);

        if (asking.Over is not { } source)
        {
            return;
        }

        var typed = asking.Value;
        var cut = typed.LastIndexOfAny(['/', '\\']);
        var folder = cut < 0 ? typed : cut == 0 ? typed[..1] : typed[..cut];
        var start = cut < 0 ? "" : typed[(cut + 1)..];

        Answers.From(
            () => Names(source, folder, start),
            found =>
            {
                if (found.Length == 0 || asking.Value != typed)
                {
                    return;
                }

                asking.Value = source.Combine(folder, found);
                asking.Caret = asking.Value.Length;
            });
    }

    /// <summary>
    /// The one name a half-typed one can only mean, or as much of it as every candidate agrees on.
    /// Completing to the longest shared beginning is what a shell does, and it is what stops the field
    /// from guessing wrong when two folders start alike.
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
            var shared = "";

            foreach (var entry in entries)
            {
                if (entry.IsParent ||
                    !entry.IsFolder ||
                    !entry.Name.StartsWith(start, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                shared = shared.Length == 0 ? entry.Name : Common(shared, entry.Name);
            }

            return shared;
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
        var shared = 0;

        while (shared < first.Length &&
               shared < second.Length &&
               char.ToLowerInvariant(first[shared]) == char.ToLowerInvariant(second[shared]))
        {
            shared++;
        }

        return first[..shared];
    }
}
