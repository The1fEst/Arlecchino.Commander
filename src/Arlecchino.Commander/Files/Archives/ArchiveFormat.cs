using System.Collections.Generic;
using System.IO;

namespace Arlecchino.Commander.Files.Archives;

/// <summary>
/// One kind of archive, which knows how to list what is in it and how to read one thing out. Nothing
/// here writes: what an archive is opened for is to look in it and to take something out.
/// </summary>
public abstract class ArchiveFormat
{
    /// <summary>Everything the archive holds.</summary>
    /// <param name="archive">The archive, as a path on this disk.</param>
    /// <returns>The entries, in whatever order the archive wrote them.</returns>
    public abstract IReadOnlyList<ArchiveEntry> Read(string archive);

    /// <summary>Opens one entry for reading.</summary>
    /// <param name="archive">The archive, as a path on this disk.</param>
    /// <param name="entry">The entry, as its path inside the archive.</param>
    /// <returns>The bytes of that entry.</returns>
    public abstract Stream Open(string archive, string entry);
}
