using System;
using Arlecchino.Commander.Stores;
using Arlecchino.Commander.Widgets.Chrome;
using Arlecchino.Input;
using Arlecchino.Layout;
using Arlecchino.Navigation;
using Arlecchino.Rendering;
using Arlecchino.State;
using static Arlecchino.Layout.PaneSplit;
using static Arlecchino.Layout.PaneTree;

namespace Arlecchino.Commander.Views;

/// <summary>
/// The frame every screen of this application is drawn inside: the band of tabs along the top, and
/// whichever view is open filling what is left. It is here rather than in each view since the tabs outlive
/// them.
/// </summary>
public class LayoutView : IArlecchinoLayout
{
    /// <summary>
    /// The cells kept clear on every side of the application. It is a constant because it moves every
    /// row and column of every screen: whatever reads the band or the bar by number has to know it.
    /// </summary>
    public const int Margin = 1;

    private readonly Banner _banner;
    private readonly Sessions _sessions;
    private readonly ArlecchinoState _state;

    private PaneTree? _pane;

    /// <summary>Puts the band of tabs above whatever view is open.</summary>
    /// <param name="sessions">Every tab and which one is showing.</param>
    /// <param name="state">What the band tells that a frame is owed, having been scrolled.</param>
    public LayoutView(Sessions sessions, ArlecchinoState state)
    {
        _state = state;
        _sessions = sessions;

        _banner = new(sessions);
    }

    /// <inheritdoc/>
    public void Draw(SurfaceRegion frame, Action<SurfaceRegion> body)
    {
        _pane ??= Lay(body);

        _pane.Draw(frame.Inset(Margin));
    }

    /// <inheritdoc/>
    public bool HandleMouse(MouseEvent mouse)
    {
        if (mouse.Action != MouseAction.Pressed || _banner.Tab(mouse.Row, mouse.Column) is not { } hit)
        {
            return false;
        }

        switch (hit.Part)
        {
            case TabPart.Fresh:
                _sessions.Add();
                return true;
            case TabPart.Close:
                _sessions.Close(_sessions.All[hit.Index]);
                return true;
            case TabPart.Scroll:
                _banner.Scroll(hit.Index);
                _state.Invalidate();
                return true;
            default:
                _sessions.Show(hit.Index);
                return true;
        }
    }

    private PaneTree Lay(Action<SurfaceRegion> body)
    {
        return Branch(
            Rows,
            PaneSize.Cells(Banner.Height),
            Leaf(_banner.Draw),
            Leaf(body));
    }
}
