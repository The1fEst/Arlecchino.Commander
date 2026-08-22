using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Ssh;

namespace Arlecchino.Commander.Files.Tty;

/// <summary>
/// A command running at a terminal of the application's own making, watched for the moment it asks for
/// the screen. What it prints goes to the roll until then; from then on the real terminal is its own.
/// </summary>
public sealed class TerminalRun : IShellRun
{
    /// <summary>How much is taken off the terminal at once.</summary>
    private const int Mouthful = 32768;

    /// <summary>What is typed at a command to tell it there is no more input coming.</summary>
    private const byte EndOfInput = 0x04;

    private readonly string _command;
    private readonly Tty? _terminal;
    private readonly Claims _claims = new();
    private readonly Says _says = new();
    private readonly byte[] _taking = new byte[Mouthful];
    private readonly byte[] _letters = new byte[Mouthful];
    private readonly byte[] _onward = new byte[Mouthful];

    private bool _lent;

    /// <summary>Starts it.</summary>
    /// <param name="command">What was typed.</param>
    /// <param name="folder">The folder to run it in.</param>
    public TerminalRun(string command, string folder)
    {
        _command = command;
        _terminal = Ttys.Local.Open(command, folder);
    }

    /// <summary>Whether a command can be run this way on this machine at all.</summary>
    public static bool Works => Ttys.Local.Works;

    /// <summary>Whether the command was started and is still going.</summary>
    public bool Listens => _terminal is { IsRunning: true };

    /// <inheritdoc/>
    public async Task ReadAsync(ShellTalk talk, CancellationToken token)
    {
        if (_terminal is not { } terminal)
        {
            talk.Prints($"[failed] {_command} could not be started");

            return;
        }

        while (true)
        {
            var count = await Task.Run(() => terminal.Read(_taking), token).ConfigureAwait(false);

            if (count <= 0)
            {
                break;
            }

            var claim = Sift(count, talk);

            if (claim < 0)
            {
                Asked(talk);

                continue;
            }

            _lent = true;

            talk.Prints($"[the screen went to {Named()}]");

            var onward = _onward;

            await talk.Lends(() => terminal.Carry(onward, claim)).ConfigureAwait(false);
        }

        _says.Rest(talk.Prints);

        talk.Prints($"[exit {terminal.Wait()}]");
    }

    /// <inheritdoc/>
    public bool Say(string line) => Typed(Encoding.UTF8.GetBytes(line + "\n"));

    /// <inheritdoc/>
    public bool EndInput() => Typed([EndOfInput]);

    /// <inheritdoc/>
    public string Interrupt()
    {
        if (_terminal is null)
        {
            return "Nothing is running";
        }

        return _terminal.Interrupt() ? "" : "Could not stop it";
    }

    /// <inheritdoc/>
    public void Dispose() => _terminal?.Dispose();

    /// <summary>
    /// Reads a mouthful of what the command printed: the letters of it into the roll and the instructions
    /// to the terminal past it. A claim among them ends the reading and is answered with what is owed.
    /// </summary>
    /// <param name="count">How much was read.</param>
    /// <param name="talk">Where the lines go.</param>
    /// <returns>How much is waiting to be passed on, or a negative number when nothing was claimed.</returns>
    private int Sift(int count, ShellTalk talk)
    {
        var letters = 0;

        for (var at = 0; at < count; at++)
        {
            switch (_claims.Takes(_taking[at]))
            {
                case Sign.Letter:
                    _letters[letters++] = _taking[at];

                    break;

                case Sign.Screen:
                    _says.Takes(_letters, letters, talk.Prints);

                    return Onward(at + 1, count);

                default:
                    break;
            }
        }

        _says.Takes(_letters, letters, talk.Prints);

        return -1;
    }

    /// <summary>
    /// Gathers what the program is owed: the instruction it claimed the screen with, and whatever it had
    /// written behind that instruction in the same mouthful.
    /// </summary>
    /// <param name="from">Just past the claim.</param>
    /// <param name="count">How much was read in all.</param>
    /// <returns>How much there is to pass on.</returns>
    private int Onward(int from, int count)
    {
        var owed = 0;

        foreach (var letter in _claims.Sequence)
        {
            _onward[owed++] = letter;
        }

        for (var at = from; at < count && owed < _onward.Length; at++)
        {
            _onward[owed++] = _taking[at];
        }

        return owed;
    }

    /// <summary>
    /// Puts to the user whatever the command has stopped on. A line it wrote and did not finish while
    /// everything went quiet is a question, read as one the way a piped command's is.
    /// </summary>
    /// <param name="talk">Where the question goes.</param>
    private void Asked(ShellTalk talk)
    {
        if (_lent || !Prompts.Asks(_says.Pending, out var prompt))
        {
            return;
        }

        _says.Rest(_ => talk.Prints(prompt));
        talk.Asks(prompt);
    }

    /// <summary>Types at the command, when there is one to type at.</summary>
    /// <param name="bytes">What to type.</param>
    /// <returns><c>true</c> when it went.</returns>
    private bool Typed(byte[] bytes) => _terminal is { } terminal && terminal.Write(bytes, bytes.Length);

    /// <summary>What to call the command in the roll, which is the word it starts with.</summary>
    /// <returns>The name.</returns>
    private string Named()
    {
        var end = _command.IndexOf(' ', StringComparison.Ordinal);

        return end < 0 ? _command : _command[..end];
    }
}
