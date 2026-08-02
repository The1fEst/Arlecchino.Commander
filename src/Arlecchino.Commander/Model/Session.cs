namespace Arlecchino.Commander.Model;

/// <summary>
/// Two panels and which of them is being worked in — one tab's worth of the application.
///
/// A connection opens a session of its own rather than taking over a panel of the one already open.
/// Half a screen is not a place to keep a server: the folder that was on that side is gone the moment
/// the connection lands, and getting it back means remembering where it was. A session leaves both of
/// them where they were and is closed whole when the server is done with.
/// </summary>
public sealed class Session
{
    /// <summary>Opens a session over two panels.</summary>
    /// <param name="left">The panel on the left.</param>
    /// <param name="right">The panel on the right.</param>
    public Session(PanelState left, PanelState right)
    {
        Left = left;
        Right = right;
    }

    /// <summary>The panel on the left.</summary>
    public PanelState Left { get; }

    /// <summary>The panel on the right.</summary>
    public PanelState Right { get; }

    /// <summary>Which side is being worked in, kept per session so a tab comes back as it was left.</summary>
    public bool RightIsActive { get; set; }

    /// <summary>Whether either panel is on a server.</summary>
    public bool IsRemote => Left.Source.IsRemote || Right.Source.IsRemote;

    /// <summary>What the left side is showing: the server it is on, or the disk.</summary>
    public string Near => Left.Source.IsRemote ? Left.Source.Label : "local";

    /// <summary>The same for the right.</summary>
    public string Far => Right.Source.IsRemote ? Right.Source.Label : "local";

    /// <summary>What the tab is called: both of its sides, since it holds both of them.</summary>
    public string Label => $"{Near} ⇄ {Far}";

    /// <summary>Closes whatever the two panels are holding open.</summary>
    public void Dispose()
    {
        Left.Source.Dispose();

        if (!ReferenceEquals(Left.Source, Right.Source))
        {
            Right.Source.Dispose();
        }
    }
}
