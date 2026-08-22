using System;
using System.Text;

namespace Arlecchino.Commander.Files.Tty;

/// <summary>
/// Gathers the letters a command printed into the lines the roll keeps. Bytes arrive in whatever lengths
/// the terminal hands over, so a letter split across two of them is held until the rest of it comes.
/// </summary>
public sealed class Says
{
    private readonly Decoder _letters = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder _pending = new();

    private char[] _room = new char[1024];
    private bool _returned;
    private string _hushedLine = "";

    /// <summary>The line the command has written and not finished, which is where a question would stand.</summary>
    public string Pending => _pending.ToString();

    /// <summary>
    /// Holds the next line back if it turns out to be this one, and only that line. A terminal that
    /// writes back what is typed at it would otherwise hand a password straight to the roll.
    /// </summary>
    /// <param name="line">What was typed, without the newline that ended it.</param>
    public void Hushes(string line) => _hushedLine = line;

    /// <summary>Takes what the command printed and gives up whatever lines it finished.</summary>
    /// <param name="bytes">The letters, with the instructions to the terminal already taken out.</param>
    /// <param name="count">How many of them.</param>
    /// <param name="line">Takes each finished line.</param>
    public void Takes(byte[] bytes, int count, Action<string> line)
    {
        if (count <= 0)
        {
            return;
        }

        if (_room.Length < count + 4)
        {
            _room = new char[count + 4];
        }

        var letters = _letters.GetChars(bytes, 0, count, _room, 0);

        for (var at = 0; at < letters; at++)
        {
            Takes(_room[at], line);
        }
    }

    /// <summary>Gives up the last line even though nothing ended it, for when there is no more coming.</summary>
    /// <param name="line">Takes the line, when there is one.</param>
    public void Rest(Action<string> line)
    {
        if (_pending.Length == 0)
        {
            return;
        }

        line(_pending.ToString());
        _pending.Clear();
    }

    /// <summary>
    /// Takes one letter. A carriage return on its own is a command drawing over the line it just wrote,
    /// so that line is begun again; one before a newline is only how a terminal ends its lines.
    /// </summary>
    /// <param name="letter">The letter.</param>
    /// <param name="line">Takes each finished line.</param>
    private void Takes(char letter, Action<string> line)
    {
        if (_returned)
        {
            _returned = false;

            if (letter != '\n')
            {
                _pending.Clear();
            }
        }

        switch (letter)
        {
            case '\r':
                _returned = true;

                return;

            case '\n':
                var whole = _pending.ToString();
                var hushedLine = _hushedLine;

                _pending.Clear();
                _hushedLine = "";

                if (hushedLine.Length == 0 || whole != hushedLine)
                {
                    line(whole);
                }

                return;

            case '\b':
                if (_pending.Length > 0)
                {
                    _pending.Length--;
                }

                return;

            default:
                if (!char.IsControl(letter) || letter == '\t')
                {
                    _pending.Append(letter);
                }

                return;
        }
    }
}
