using System;

namespace Arlecchino.Commander.Files.Trash;

/// <summary>
/// Where a deleted file goes when it is not meant to be gone, which every desktop spells differently. A
/// server has nothing of the sort, so this is only ever about a disk on this machine.
/// </summary>
public abstract class Trash
{
    /// <summary>The one this machine has.</summary>
    public static Trash Here { get; } = Pick();

    /// <summary>Whether there is one at all, which decides whether the panel offers it.</summary>
    public abstract bool Works { get; }

    /// <summary>
    /// Puts a file or a folder where the user could get it back from.
    /// </summary>
    /// <param name="path">What to put away.</param>
    /// <returns><c>false</c> when it could not be, and it is still where it was.</returns>
    public abstract bool TryPut(string path);

    private static Trash Pick()
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsTrash.Instance;
        }

        if (OperatingSystem.IsMacOS())
        {
            return MacTrash.Instance;
        }

        return OperatingSystem.IsLinux() ? FreedesktopTrash.Instance : NoTrash.Instance;
    }
}

/// <summary>
/// A machine with nowhere to put things. Deleting there is what it has always been, and the panel says
/// so rather than pretending a file went somewhere it could be fetched from.
/// </summary>
public sealed class NoTrash : Trash
{
    /// <summary>The one of these there is.</summary>
    public static readonly NoTrash Instance = new();

    /// <inheritdoc/>
    public override bool Works => false;

    /// <inheritdoc/>
    public override bool TryPut(string path) => false;
}
