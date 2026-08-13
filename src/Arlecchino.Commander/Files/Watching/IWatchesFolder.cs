using System;

namespace Arlecchino.Commander.Files.Watching;

/// <summary>
/// A source the machine tells about changes, so a panel on it is never read again for nothing. A source
/// without this is watched by being read again every so often instead.
/// </summary>
public interface IWatchesFolder
{
    /// <summary>Starts saying when anything in a folder changes, leaving what is under it alone.</summary>
    /// <param name="folder">The folder to watch.</param>
    /// <param name="changed">Called on whichever thread noticed, as often as the machine says so.</param>
    /// <returns>What stops the watching, or <c>null</c> when this folder cannot be watched.</returns>
    IDisposable? Watch(string folder, Action changed);
}
