using System;
using System.ComponentModel;
using System.Diagnostics;
using Arlecchino.Commander.Model;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Hosting;
using Arlecchino.State;

namespace Arlecchino.Commander.Views.Doing;

/// <summary>
/// Opens the file under the cursor in the editor the settings name, stepping the screen aside until it
/// exits. Only files on this machine: a panel showing a server says so instead.
/// </summary>
public sealed class Editing
{
    private readonly Handover _handover;
    private readonly Settings _settings;
    private readonly ArlecchinoState _state;
    private readonly Pair _panels;

    /// <summary>Gathers what editing needs.</summary>
    /// <param name="handover">What lends the terminal to the editor and takes it back.</param>
    /// <param name="settings">Where the editor to run is named.</param>
    /// <param name="state">Where what happened is said.</param>
    /// <param name="panels">The two panels on screen.</param>
    public Editing(Handover handover, Settings settings, ArlecchinoState state, Pair panels)
    {
        _handover = handover;
        _settings = settings;
        _state = state;
        _panels = panels;
    }

    /// <summary>
    /// Edits what the cursor is on. The panel is read again afterward however it went: an editor that
    /// wrote the file changed its size and its date, and one that wrote a new file beside it changed the
    /// folder.
    /// </summary>
    public void Edit()
    {
        var panel = _panels.Active;

        if (panel.Current is not { IsFolder: false } current)
        {
            _state.Output = Loc(LocString.SaidNothingToEdit);

            return;
        }

        if (panel.Source.IsRemote)
        {
            _state.Output = Loc(LocString.SaidEditsHereOnly);

            return;
        }

        if (_settings.Editor is not { Length: > 0 } editor)
        {
            _state.Output = Loc(LocString.SaidNoEditor);

            return;
        }

        Run(editor, current, panel.Folder);
        panel.Reload();
    }

    /// <summary>
    /// Hands the terminal over and runs it. An editor that is missing, that will not start or that exits
    /// complaining is reported on the screen rather than thrown.
    /// </summary>
    /// <param name="editor">What was set, which may carry arguments of its own.</param>
    /// <param name="file">The file to open.</param>
    /// <param name="folder">The folder to start it in.</param>
    private void Run(string editor, FileEntry file, string folder)
    {
        var words = editor.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length == 0)
        {
            _state.Output = Loc(LocString.SaidNoEditor);

            return;
        }

        var start = new ProcessStartInfo(words[0]) { WorkingDirectory = folder };

        for (var at = 1; at < words.Length; at++)
        {
            start.ArgumentList.Add(words[at]);
        }

        start.ArgumentList.Add(file.Path);

        try
        {
            var code = _handover.Run(start);

            _state.Output = code == 0
                ? Loc(LocString.SaidEdited, file.Name)
                : Loc(LocString.SaidEditorRefused, words[0], code);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or
                                          PlatformNotSupportedException)
        {
            _state.Output = Loc(LocString.SaidEditorMissing, words[0]);
        }
    }
}
