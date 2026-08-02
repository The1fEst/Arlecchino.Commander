using System;
using System.Collections.Generic;

namespace Arlecchino.Commander.Model;

/// <summary>
/// One entry of a menu: which string names it, and what choosing it does.
///
/// The two used to be written apart — a list of names in one place, a switch over the same names in
/// another — and the only thing holding them together was that nobody had renamed one without the
/// other yet. An entry that carries what it does cannot be listed and left unwired.
/// </summary>
/// <param name="Name">Which string names it.</param>
/// <param name="Run">What choosing it does.</param>
public sealed record MenuItem(LocString Name, Action Run);

/// <summary>One menu and what is on it.</summary>
/// <param name="Title">Which string names the menu.</param>
/// <param name="Items">What is on it.</param>
public sealed record MenuSection(LocString Title, IReadOnlyList<MenuItem> Items);
