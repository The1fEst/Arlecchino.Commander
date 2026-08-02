using System.Collections.Generic;

namespace Arlecchino.Commander.Model;

/// <summary>
/// One menu and what is on it, named rather than worded. The words are looked up where the menu is
/// drawn; what is stored is which string each entry is, so the entry a choice runs is the entry the
/// menu listed and no comparison of sentences stands between them.
/// </summary>
/// <param name="Title">Which string names the menu.</param>
/// <param name="Items">Which strings name what is on it.</param>
public sealed record MenuSection(LocString Title, IReadOnlyList<LocString> Items);
