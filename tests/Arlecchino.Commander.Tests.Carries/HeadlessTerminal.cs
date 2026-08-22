using System.IO;
using System.Text;
using System.Threading;
using Arlecchino.Commander.Files.Tty;

namespace Arlecchino.Commander.Tests.Carries;

/// <summary>
/// A pair of ends opened by the test itself, with the try at the far end of it. Nothing is drawn on a
/// screen and no one need be logged in, so this is the one a build server tries.
/// </summary>
internal sealed class HeadlessTerminal : OpenedTerminal
{
    /// <summary>How much is taken off the pair at once while it is drained.</summary>
    private const int Mouthful = 4096;

    private Tty? _pair;

    /// <inheritdoc/>
    internal override string? Missing() =>
        Ttys.Local.Works ? null : "this machine makes no terminal of its own";

    /// <summary>
    /// Opens the pair and puts the try at the far end of it. What the try prints is read and thrown
    /// away, since a pair nothing reads fills up and stops the program writing into it.
    /// </summary>
    /// <param name="shell">Which shell.</param>
    /// <param name="command">The try and what to tell it.</param>
    /// <returns><c>true</c> when the pair was opened.</returns>
    internal override bool Opens(string shell, string command)
    {
        _pair = Ttys.Local.Open($"exec {shell} -c {Processes.Quoted(command)}", Path.GetTempPath());

        if (_pair is not { } pair)
        {
            return false;
        }

        var draining = new Thread(() =>
        {
            var mouthful = new byte[Mouthful];

            while (pair.Read(mouthful) > 0) { }
        })
        {
            IsBackground = true,
        };

        draining.Start();

        return true;
    }

    /// <summary>
    /// Types the key at the near end, which is the keyboard of this terminal and is held right here.
    /// </summary>
    /// <param name="letter">The key.</param>
    internal override void Presses(char letter)
    {
        var typing = Encoding.UTF8.GetBytes(letter.ToString());

        _ = _pair?.Write(typing, typing.Length);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _pair?.Dispose();
        _pair = null;
    }
}
