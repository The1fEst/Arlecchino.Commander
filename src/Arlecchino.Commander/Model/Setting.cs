using System;
using System.Collections.Generic;

namespace Arlecchino.Commander.Model;

/// <summary>
/// One thing that can be set and kept: the word it is named by, the string saying what it is for, and what
/// to offer as a value. The name is the same word whatever language the screen is in.
/// </summary>
/// <param name="Name">The word typed to set it.</param>
/// <param name="About">Which string says what it is for.</param>
/// <param name="Suggest">What to offer as values, asked when the hints are drawn.</param>
public sealed record Setting(string Name, LocString About, Func<IReadOnlyList<string>> Suggest);
