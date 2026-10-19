using System.Collections.Generic;
using Arlecchino.Commander.Files.Opening;

namespace Arlecchino.Commander.Tests.Support;

/// <summary>
/// An opener that writes down what it was handed and starts nothing. A test host has no desktop, and a
/// key that really opened a file would leave a window behind it on the machine running the tests.
/// </summary>
public sealed class NotedOpener : Opener
{
    private readonly List<string> _paths = [];

    /// <summary>Whether this one opens anything, which a test turns off to stand for a bare machine.</summary>
    public bool Opens { get; set; } = true;

    /// <summary>Paths this one fails to open while opening the rest, as a file with no handler would.</summary>
    public HashSet<string> RefusedPaths { get; } = [];

    /// <summary>Every path handed over, in the order it was.</summary>
    public IReadOnlyList<string> Paths => _paths;

    /// <inheritdoc/>
    public override bool Works => Opens;

    /// <inheritdoc/>
    public override bool TryOpen(string path)
    {
        _paths.Add(path);

        return Opens && !RefusedPaths.Contains(path);
    }
}
