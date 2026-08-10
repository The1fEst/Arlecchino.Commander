using System;
using System.Collections.Generic;

namespace Arlecchino.Commander.Model;

/// <summary>
/// One thing that can be set and kept: the word it is named by on the settings line, the string that
/// says what it is for, and what it is worth offering as a value.
///
/// The name is not localized and the description is. The name is typed, so it has to be the same word
/// whatever language the screen is in. A setting file that says <c>editor</c> in one language and
/// something else in another cannot be shared between two machines.
///
/// What to suggest is asked for rather than listed, because the useful answer is not a constant: the
/// editors worth offering are the ones this machine actually has.
/// </summary>
/// <param name="Name">The word typed to set it.</param>
/// <param name="About">Which string says what it is for.</param>
/// <param name="Suggest">What to offer as values, asked when the hints are drawn.</param>
public sealed record Setting(string Name, LocString About, Func<IReadOnlyList<string>> Suggest);
