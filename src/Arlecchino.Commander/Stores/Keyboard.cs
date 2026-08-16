using System.Collections.Generic;
using Arlecchino.Atoms;
using Arlecchino.Commands;

namespace Arlecchino.Commander.Stores;

/// <summary>
/// The table of keys the panels answer to, left where another screen can read it. It is built against
/// everything the panels can do, and the screen listing the keys can do none of that.
/// </summary>
public sealed class Keyboard : IArlecchinoStore
{
    /// <summary>Every key the panels answer to, as they last built the table.</summary>
    public IReadOnlyList<ViewCommand> Panels { get; set; } = [];
}
