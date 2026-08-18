using System;
using Arlecchino.Editing;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// Words as a command line has them: told apart by the spaces between them, except inside a pair of quotes,
/// where a name is allowed its own spaces. A quote left open takes the word back to the quote itself.
/// </summary>
public sealed class ShellWords : ICutsWords
{
    /// <inheritdoc/>
    public CompletionAsk Cut(string text, int caret)
    {
        var end = Math.Clamp(caret, 0, text.Length);
        var stream = Opened(text, end);

        if (stream >= 0)
        {
            return new(text, stream, end - stream);
        }

        var start = end;

        while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
        {
            start--;
        }

        return new(text, start, end - start);
    }

    /// <summary>Where the quote that was never closed stands, counting from the start of the line.</summary>
    /// <param name="text">The line.</param>
    /// <param name="end">How far along to read, which is where the caret is.</param>
    /// <returns>Where it is, or <c>-1</c> when every quote was closed.</returns>
    private static int Opened(string text, int end)
    {
        var stream = -1;

        for (var at = 0; at < end; at++)
        {
            if (text[at] == '"')
            {
                stream = stream < 0 ? at : -1;
            }
        }

        return stream;
    }
}
