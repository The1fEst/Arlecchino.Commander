using System;
using System.Collections.Generic;
using System.IO;
using Arlecchino.Atoms;
using Arlecchino.Atoms.Local;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Stores;

public sealed class Sessions : IArlecchinoStore
{
    private readonly List<Session> _sessions =
    [
        new(new(new LocalSource(), Directory.GetCurrentDirectory()), new(new LocalSource(), Listing.Home())),
    ];

    /// <summary>The sessions there are, in the order their tabs are drawn.</summary>
    public IReadOnlyList<Session> All => _sessions;

    /// <summary>
    /// Which session the screen is showing. An atom, so opening a tab or stepping between them marks the
    /// frame stale by itself rather than having to be announced.
    /// </summary>
    public Atom<int> Open { get; } = new LocalAtom<int>(0);

    /// <summary>The session on screen.</summary>
    public Session Current => _sessions[Math.Clamp(Open.Value, 0, _sessions.Count - 1)];

    public PanelState Left => Current.Left;

    public PanelState Right => Current.Right;

    public Atom<bool> RightIsActive { get; } = new LocalAtom<bool>(false);

    public Atom<string> Viewing { get; } = new LocalAtom<string>("");

    public IFileSource ViewingSource { get; set; } = new LocalSource();

    public long ViewingSize { get; set; }

    /// <summary>Folders kept by hand for jumping straight back to them.</summary>
    public List<string> Hotlist { get; } = [];

    /// <summary>
    /// Counts the times a panel was sent somewhere by a screen other than the panels themselves. That screen
    /// can then put the cursor where it likes, and the panels catch up when they are next drawn.
    /// </summary>
    public Atom<int> Revision { get; } = new LocalAtom<int>(0);

    public void Moved() => Revision.Value++;

    /// <summary>
    /// Opens another tab, with two panels of its own on the folders this one is showing. Connecting
    /// does not do this — a connection lands in the panel that asked for it — so a tab is opened only
    /// when somebody wants a second pair of panels to work in.
    /// </summary>
    public void Add()
    {
        Current.RightIsActive = RightIsActive.Value;
        _sessions.Add(new(new(new LocalSource(), Left.Folder), new(new LocalSource(), Right.Folder)));

        Open.Value = _sessions.Count - 1;
        RightIsActive.Value = false;
    }

    /// <summary>
    /// Closes a tab and everything its panels were holding open. The last one stays: a file manager
    /// with no panels is not a state worth being able to reach.
    /// </summary>
    /// <param name="session">Which one.</param>
    /// <returns><c>true</c> when it was closed.</returns>
    public bool Close(Session session)
    {
        if (_sessions.Count == 1 || !_sessions.Remove(session))
        {
            return false;
        }

        session.Dispose();

        Open.Value = Math.Clamp(Open.Value, 0, _sessions.Count - 1);
        RightIsActive.Value = Current.RightIsActive;

        return true;
    }

    /// <summary>Goes to the tab beside this one, wrapping round at either end.</summary>
    /// <param name="forward">Which way.</param>
    public void Step(bool forward)
    {
        if (_sessions.Count > 1)
        {
            Show(((Open.Value + (forward ? 1 : -1)) + _sessions.Count) % _sessions.Count);
        }
    }

    /// <summary>
    /// Shows a session, remembering which side the one being left was working in. The side belongs to
    /// the tab rather than to the application, so a tab comes back the way it was left.
    /// </summary>
    /// <param name="index">Which session.</param>
    public void Show(int index)
    {
        if (index < 0 || index >= _sessions.Count || index == Open.Value)
        {
            return;
        }

        Current.RightIsActive = RightIsActive.Value;
        Open.Value = index;
        RightIsActive.Value = Current.RightIsActive;
    }

    public void Start(string left, string right)
    {
        if (Directory.Exists(left))
        {
            Left.GoTo(Path.GetFullPath(left));
        }

        if (Directory.Exists(right))
        {
            Right.GoTo(Path.GetFullPath(right));
        }
    }
}
