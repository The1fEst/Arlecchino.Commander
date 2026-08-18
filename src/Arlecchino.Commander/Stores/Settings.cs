using System;
using System.Collections.Generic;
using System.Globalization;
using Arlecchino.Atoms;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Stores;

/// <summary>
/// What is kept between runs, read once as the application starts and written back the moment something
/// is changed. Values are held by name, so the settings line can offer, read and change any of them.
/// </summary>
public sealed class Settings : IArlecchinoStore
{
    /// <summary>The name the editor is set under, on the line and in the file.</summary>
    public const string EditorName = "editor";

    /// <summary>The name the watching is set under, in seconds or as the word for none.</summary>
    public const string WatchName = "watch";

    /// <summary>The word that turns the watching off, typed in place of a number of seconds.</summary>
    public const string NoWatch = "off";

    private static readonly string[] KnownEditors =
        ["vim", "nvim", "helix", "hx", "micro", "nano", "kak", "emacs", "vi", "code", "subl"];

    private static readonly string[] KnownWatches = [NoWatch, "2", "5", "10", "30"];

    private static readonly TimeSpan WatchByDefault = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan Fastest = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan Slowest = TimeSpan.FromHours(1);

    private readonly Dictionary<string, string> _values;

    /// <summary>Reads whatever was kept last time.</summary>
    public Settings()
    {
        Place = SettingsFile.Place();
        _values = SettingsFile.Read(Place);

        if (!_values.ContainsKey(EditorName) && FromEnvironment() is { Length: > 0 } entry)
        {
            _values[EditorName] = entry;
        }

        All =
        [
            new(EditorName, LocString.SettingsEditor, static () => Programs.Present(KnownEditors)),
            new(WatchName, LocString.SettingsWatch, static () => KnownWatches),
        ];
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

    /// <summary>
    /// How often a panel on a server reads its folder again, and whether the panels watch at all. Nothing
    /// at all stops the watching, the disk included; anything else is a number of seconds, up to an hour.
    /// </summary>
    public TimeSpan Watch
    {
        get
        {
            var value = Value(WatchName);

            if (value.Length == 0)
            {
                return WatchByDefault;
            }

            if (value.Equals(NoWatch, StringComparison.OrdinalIgnoreCase))
            {
                return TimeSpan.Zero;
            }

            return double.TryParse(value, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? TimeSpan.FromSeconds(Math.Clamp(seconds, Fastest.TotalSeconds, Slowest.TotalSeconds))
                : TimeSpan.Zero;
        }
    }

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
    /// Changes a setting and writes the file. The change is in force whether the file could be written or
    /// not, so a read-only home folder is reported rather than refused.
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
    /// What the environment says an editor is, which is what every other terminal program would open. It
    /// is taken as the value rather than as a fallback, so the settings line shows what is in force.
    /// </summary>
    /// <returns>The program, or an empty string when neither variable is set.</returns>
    private static string FromEnvironment() =>
        Environment.GetEnvironmentVariable("VISUAL") is { Length: > 0 } visual
            ? visual
            : Environment.GetEnvironmentVariable("EDITOR") ?? "";
}
