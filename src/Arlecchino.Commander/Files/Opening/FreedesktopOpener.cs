using System;
using System.Diagnostics;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Files.Opening;

/// <summary>
/// The handler the Linux desktops share, which is a program on the path rather than a call. It reads
/// the <c>.desktop</c> files itself and starts whichever of them claims the type.
/// </summary>
public sealed class FreedesktopOpener : Opener
{
    /// <summary>
    /// What a desktop is asked through, in the order they are tried. The first is what the
    /// specification names; the second comes with glib, on a machine where the first was never put.
    /// </summary>
    private static readonly (string Program, string[] Words)[] Handlers =
    [
        ("xdg-open", []),
        ("gio", ["open"]),
    ];

    private readonly string _program;

    private readonly string[] _words;

    /// <summary>Points this at one handler, which is how a test gets one whatever the machine has.</summary>
    /// <param name="program">The program to run.</param>
    /// <param name="words">What it wants before the path.</param>
    public FreedesktopOpener(string program, params string[] words)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(program);

        _program = program;
        _words = words;
    }

    /// <summary>The handler this machine has, or none when it has neither.</summary>
    public static Opener Instance { get; } = Discover();

    /// <inheritdoc/>
    public override ProcessStartInfo Starting(string path)
    {
        var start = new ProcessStartInfo(_program)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var word in _words)
        {
            start.ArgumentList.Add(word);
        }

        start.ArgumentList.Add(path);

        return start;
    }

    private static Opener Discover()
    {
        foreach (var (program, words) in Handlers)
        {
            if (Programs.Present([program]).Count > 0)
            {
                return new FreedesktopOpener(program, words);
            }
        }

        return NoOpener.Instance;
    }
}
