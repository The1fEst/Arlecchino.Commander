using System;

namespace Arlecchino.Commander.Files.Archives;

/// <summary>
/// One entry as an archive lists it, before anything is made of it. The path is the whole path inside
/// the archive, which is how every format writes it and is not a tree until something makes one.
/// </summary>
/// <param name="Path">Where it sits inside the archive, as <c>src/main.c</c>.</param>
/// <param name="Size">How large it is once unpacked, in bytes.</param>
/// <param name="Modified">When it was last written, as the archive recorded it.</param>
/// <param name="IsFolder">Whether the entry is a folder rather than a file.</param>
public sealed record ArchiveEntry(string Path, long Size, DateTime Modified, bool IsFolder);
