using System;
using System.Collections.Generic;

namespace Arlecchino.Commander.Files.Tty;

/// <summary>What a byte from a command turned out to be.</summary>
public enum Sign
{
    /// <summary>A letter of what the command is saying.</summary>
    Letter,

    /// <summary>Part of an instruction to the terminal, which is not for the reading of it.</summary>
    Part,

    /// <summary>The end of an instruction that only a program drawing on the screen would send.</summary>
    Screen,
}

/// <summary>
/// Reads what a command prints one byte at a time and tells the letters from the instructions. What it
/// watches for among the instructions is the command asking for the screen.
/// </summary>
public sealed class Claims
{
    private const byte Escape = 0x1B;
    private const byte Bell = 0x07;
    private const byte Backslash = (byte)'\\';

    /// <summary>
    /// What a program turns on when the terminal is to be its own: the second screen a terminal keeps,
    /// the mouse, and the keyboard in the mode that sends the arrows as sequences.
    /// </summary>
    private static readonly int[] Owning = [1, 47, 1047, 1049, 1000, 1002, 1003, 1006];

    private readonly List<byte> _sequence = [];

    private Reading _reading;
    private bool _opening;

    /// <summary>Reads what the commands at one terminal print.</summary>
    /// <param name="blanks">
    /// Whether that terminal paints itself blank before the command has written a word. Where it does,
    /// the clearing and the cursor sent home at the head of everything are the terminal's own doing, and
    /// a command that has not written a letter yet has claimed nothing by them.
    /// </param>
    public Claims(bool blanks) => _opening = blanks;

    /// <summary>
    /// The instruction read so far, whole once the screen has been claimed. It is written to the real
    /// terminal as it stands, since the program that sent it is owed the terminal it was sent to.
    /// </summary>
    public IReadOnlyList<byte> Sequence => _sequence;

    /// <summary>Takes the next byte and says what it was.</summary>
    /// <param name="letter">The byte.</param>
    /// <returns>What it turned out to be.</returns>
    public Sign Takes(byte letter)
    {
        switch (_reading)
        {
            case Reading.Text:
                if (letter != Escape)
                {
                    _opening = false;

                    return Sign.Letter;
                }

                _sequence.Clear();
                _sequence.Add(letter);
                _reading = Reading.Escaped;

                return Sign.Part;

            case Reading.Escaped:
                _sequence.Add(letter);
                _reading = letter switch
                {
                    (byte)'[' => Reading.Inside,
                    (byte)']' or (byte)'P' or (byte)'X' or (byte)'^' or (byte)'_' => Reading.Stringly,
                    (byte)'(' or (byte)')' or (byte)'*' or (byte)'+' or (byte)'%' or (byte)'#' => Reading.Skipping,
                    _ => Reading.Text,
                };

                return Sign.Part;

            case Reading.Inside:
                _sequence.Add(letter);

                if (letter is < 0x40 or > 0x7E)
                {
                    return Sign.Part;
                }

                _reading = Reading.Text;

                return Claimed() ? Sign.Screen : Sign.Part;

            case Reading.Stringly:
                _sequence.Add(letter);

                if (letter == Bell)
                {
                    _reading = Reading.Text;
                }
                else if (letter == Escape)
                {
                    _reading = Reading.Ending;
                }

                return Sign.Part;

            case Reading.Ending:
                _sequence.Add(letter);
                _reading = letter == Backslash ? Reading.Text : Reading.Stringly;

                return Sign.Part;

            default:
                _sequence.Add(letter);
                _reading = Reading.Text;

                return Sign.Part;
        }
    }

    /// <summary>
    /// Whether the instruction just read is a command asking for the screen, which it does in one of
    /// four ways. A terminal that opens blank does two of those itself, and they count only after the
    /// command writes a letter.
    /// </summary>
    /// <returns><c>true</c> when the screen has been claimed.</returns>
    private bool Claimed()
    {
        var last = _sequence[^1];
        var privately = _sequence.Count > 2 && _sequence[2] is (byte)'?';

        if (last == (byte)'c')
        {
            return true;
        }

        if (privately)
        {
            return last == (byte)'h' && Numbers().Exists(static number => Array.IndexOf(Owning, number) >= 0);
        }

        return last switch
        {
            (byte)'n' => Numbers().Contains(6),
            (byte)'H' or (byte)'f' => !_opening,
            (byte)'J' => !_opening && Numbers().Exists(static number => number is 2 or 3),
            _ => false,
        };
    }

    /// <summary>The numbers in the instruction, of which there may be several.</summary>
    /// <returns>The numbers.</returns>
    private List<int> Numbers()
    {
        var numbers = new List<int>();
        var number = -1;

        foreach (var letter in _sequence)
        {
            if (letter is >= (byte)'0' and <= (byte)'9')
            {
                number = Math.Max(number, 0) * 10 + (letter - '0');

                continue;
            }

            if (number >= 0)
            {
                numbers.Add(number);
                number = -1;
            }
        }

        if (number >= 0)
        {
            numbers.Add(number);
        }

        return numbers;
    }

    /// <summary>Where in an instruction the reading has got to.</summary>
    private enum Reading
    {
        /// <summary>Between instructions, where everything is a letter.</summary>
        Text,

        /// <summary>Just after the escape that begins one.</summary>
        Escaped,

        /// <summary>Inside the common sort, which ends at the first letter that could be its name.</summary>
        Inside,

        /// <summary>Inside the sort that carries text of its own and ends at a bell or a backslash.</summary>
        Stringly,

        /// <summary>Just after the escape that may be ending one of those.</summary>
        Ending,

        /// <summary>Inside a short one, of which one more byte is left.</summary>
        Skipping,
    }
}
