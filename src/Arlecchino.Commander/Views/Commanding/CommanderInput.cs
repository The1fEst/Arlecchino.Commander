using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Panels;
using Arlecchino.Input;
using Arlecchino.Navigation;

namespace Arlecchino.Commander.Views.Commanding;

/// <summary>
///     Who gets the key. A search running on a panel takes what it types before anything else; otherwise the
///     lines under the panels are offered it, and whatever is left goes to the focus.
/// </summary>
public sealed class CommanderInput
{
    private readonly CommanderFooter _footer;
    private readonly CommanderLayout _layout;
    private readonly Pair _panels;
    private readonly Sessions _sessions;

    /// <summary>Routes what arrives at the screen.</summary>
    /// <param name="panels">The two panels on screen.</param>
    /// <param name="footer">The lines under the panels.</param>
    /// <param name="layout">Where the focus is.</param>
    /// <param name="sessions">Which side is being worked in, which is written down as the focus moves.</param>
    public CommanderInput(Pair panels, CommanderFooter footer, CommanderLayout layout, Sessions sessions)
    {
        _panels = panels;
        _footer = footer;
        _layout = layout;
        _sessions = sessions;
    }

    /// <summary>Offers the key to the lines under the panels, and to the focus after them.</summary>
    /// <param name="key">The key that arrived.</param>
    /// <returns>Where to go, which is nowhere for everything a line took.</returns>
    public ViewRoute Handle(KeyPress key)
    {
        if (!_panels.Active.IsSearching && _footer.Handle(key))
        {
            return ViewRoute.None;
        }

        return Routed(_layout.Handle(key));
    }

    /// <summary>
    ///     Text pasted into the terminal, which goes where typing goes: the search running on the panel when
    ///     there is one, and the lines under the panels otherwise.
    /// </summary>
    /// <param name="text">What was pasted, with the terminal's markers already stripped.</param>
    /// <returns>Where to go, which is nowhere.</returns>
    public ViewRoute HandlePaste(string text)
    {
        if (!_panels.Active.Paste(text))
        {
            _footer.Paste(text);
        }

        return ViewRoute.None;
    }

    /// <summary>
    ///     Clicks, which go to whichever panel was clicked in. The band along the top belongs to the layout,
    ///     which is asked before the screen is.
    /// </summary>
    /// <param name="mouse">The event that arrived.</param>
    /// <returns>Where to go, which is nowhere.</returns>
    public ViewRoute HandleMouse(MouseEvent mouse)
    {
        return Routed(_layout.HandleMouse(mouse));
    }

    /// <summary>Remembers which side the focus landed on, so the tab comes back as it was left.</summary>
    /// <param name="route">Where whatever took the event wants to go.</param>
    /// <returns>The same.</returns>
    private ViewRoute Routed(ViewRoute route)
    {
        _sessions.RightIsActive.Value = _panels.Right.IsFocused;

        return route;
    }
}
