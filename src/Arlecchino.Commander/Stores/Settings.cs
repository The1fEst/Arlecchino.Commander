using System;
using System.Collections.Generic;
using Arlecchino.Atoms;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Stores;

/// <summary>
/// What is kept between runs, read once as the application starts and written back the moment
/// something is changed.
///
/// Writing on every change rather than on the way out is deliberate. A file manager is quit by killing
/// the terminal it is in as often as by pressing the key for it, and a setting that only survives a
/// polite exit is a setting nobody trusts.
///
/// Values are held by name rather than as fields, so the settings line can offer, read and change any
/// of them without a switch that has to grow a case each time one is added. What each name means to the
/// rest of the application is a property here, and that is the only place a name is spelled twice.
/// </summary>
public sealed class Settings : IArlecchinoStore
{
    /// <summary>The name the editor is set under, on the line and in the file.</summary>
    public const string EditorName = "editor";

    private static readonly string[] KnownEditors =
        ["vim", "nvim", "helix", "hx", "micro", "nano", "kak", "emacs", "vi", "code", "subl"];

    private readonly Dictionary<string, string> _values;

    /// <summary>Reads whatever was kept last time.</summary>
    public Settings()
    {
        Place = SettingsFile.Place();
        _values = SettingsFile.Read(Place);

        if (!_values.ContainsKey(EditorName) && FromEnvironment() is { Length: > 0 } named)
        {
            _values[EditorName] = named;
        }

        All = [new(EditorName, LocString.SettingsEditor, static () => Programs.Present(KnownEditors))];
    }

    /// <summary>The file all of this is kept in, named on screen so it can be edited by hand.</summary>
    public string Place { get; }

    /// <summary>Everything that can be set, in the order the settings line offers them.</summary>
    public IReadOnlyList<Setting> All { get; }

    /// <summary>
    /// The program a file is opened for editing in. Empty when nothing has been set and the environment
    /// names nothing either, which is the one case the editing key has to say something about rather
    /// than act on.
    /// </summary>
    public string Editor => Value(EditorName);

    /// <summary>What a setting is now.</summary>
    /// <param name="name">Which setting.</param>
    /// <returns>Its value, or an empty string when it has never been set.</returns>
    public string Value(string name) => _values.GetValueOrDefault(name, "");

    /// <summary>The setting of that name, whatever case it was typed in.</summary>
    /// <param name="name">What was typed.</param>
    /// <returns>The setting, or <c>null</c> when nothing is called that.</returns>
    public Setting? Named(string name)
    {
        foreach (var setting in All)
        {
            if (setting.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return setting;
            }
        }

        return null;
    }

    /// <summary>
    /// Changes a setting and writes the file. The change is in force whether the file could be written
    /// or not — a read-only home folder is a reason to say so, not a reason to refuse the setting for
    /// the rest of the session.
    /// </summary>
    /// <param name="name">Which setting.</param>
    /// <param name="value">What it should be.</param>
    /// <returns><c>true</c> when it was written down as well as taken.</returns>
    public bool Set(string name, string value)
    {
        _values[name] = value;

        return SettingsFile.Write(Place, _values);
    }

    /// <summary>
    /// What the environment says an editor is, which is what every other terminal program would open.
    /// It is taken as the value rather than as a fallback behind one, so the settings line shows what is
    /// really in force instead of the word "unset" beside an editor that works.
    /// </summary>
    /// <returns>The program, or an empty string when neither variable is set.</returns>
    private static string FromEnvironment() =>
        Environment.GetEnvironmentVariable("VISUAL") is { Length: > 0 } visual
            ? visual
            : Environment.GetEnvironmentVariable("EDITOR") ?? "";
}
