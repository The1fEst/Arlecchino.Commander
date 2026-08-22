using System;
using System.Text;

namespace Arlecchino.Commander.Files.Ssh;

/// <summary>
/// Telling a question a command has stopped on from the rest of what it prints. A question is a line
/// that never ends: the command writes it and waits, so it stands unfinished while everything is quiet.
/// </summary>
public static class Prompts
{
    /// <summary>How long a line can be and still be a question rather than a paragraph.</summary>
    private const int MostRoom = 160;

    /// <summary>
    /// Whether what a command has left hanging is a question. It is read off the shape of the line rather
    /// than off the words on it, so it holds whatever language the command stopped in.
    /// </summary>
    /// <param name="pending">The line the command has written and not finished.</param>
    /// <param name="prompt">The question, tidied of the space a prompt is usually followed by.</param>
    /// <returns><c>true</c> when there is a question to put to the user.</returns>
    public static bool Asks(string pending, out string prompt)
    {
        prompt = pending.Trim();

        return prompt.Length is > 0 and <= MostRoom && prompt.EndsWith(':');
    }

    /// <summary>
    /// Spells a command so that <c>sudo</c> asks over the pipe rather than at a terminal. Left as it was,
    /// it looks for a terminal this application has none of and gives up before it has asked anything.
    /// </summary>
    /// <param name="command">What was typed.</param>
    /// <returns>The command as it is run, which is the same one wherever <c>sudo</c> is not in it.</returns>
    public static string Piped(string command)
    {
        var spelledCommand = new StringBuilder(command.Length + 3);
        var fresh = true;
        var at = 0;

        while (at < command.Length)
        {
            var character = command[at];

            if (character is '|' or '&' or ';')
            {
                spelledCommand.Append(character);
                fresh = true;
                at++;

                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                spelledCommand.Append(character);
                at++;

                continue;
            }

            var end = Word(command, at);
            var word = command[at..end];

            spelledCommand.Append(fresh && word == "sudo" && !Answers(command, end) ? "sudo -S" : word);

            fresh = false;
            at = end;
        }

        return spelledCommand.ToString();
    }

    /// <summary>
    /// How far one word of a command line reaches. A quoted piece is one word however much is inside it,
    /// so that what a <c>;</c> means is read off the command rather than off the text of an argument.
    /// </summary>
    /// <param name="command">What was typed.</param>
    /// <param name="from">Where the word starts.</param>
    /// <returns>Where it ends.</returns>
    private static int Word(string command, int from)
    {
        var at = from;

        while (at < command.Length && !char.IsWhiteSpace(command[at]) && command[at] is not ('|' or '&' or ';'))
        {
            if (command[at] is not ('"' or '\''))
            {
                at++;

                continue;
            }

            var closing = command.IndexOf(command[at], at + 1);

            at = closing < 0 ? command.Length : closing + 1;
        }

        return at;
    }

    /// <summary>
    /// Whether a <c>sudo</c> has already been told where to get its answer from — over the pipe, from a
    /// program of its own, or not to ask at all. Any of the three is an instruction to leave it alone.
    /// </summary>
    /// <param name="command">What was typed.</param>
    /// <param name="from">Just after the word <c>sudo</c>.</param>
    /// <returns><c>true</c> when nothing should be added.</returns>
    private static bool Answers(string command, int from)
    {
        var at = from;

        while (at < command.Length)
        {
            while (at < command.Length && command[at] == ' ')
            {
                at++;
            }

            if (at >= command.Length || command[at] != '-')
            {
                return false;
            }

            var end = Word(command, at);
            var option = command[at..end];

            if (option is "--stdin" or "--askpass" or "--non-interactive" ||
                (!option.StartsWith("--", StringComparison.Ordinal) && option.AsSpan().IndexOfAny("SAn") >= 0))
            {
                return true;
            }

            at = end;
        }

        return false;
    }
}
