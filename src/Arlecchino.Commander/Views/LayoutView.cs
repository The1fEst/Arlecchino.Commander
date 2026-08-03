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

public class LayoutView : IArlecchinoLayout
{
    private readonly Banner _banner;
    private readonly Sessions _sessions;
    private readonly ArlecchinoState _state;

    private PaneTree? _pane;

    public LayoutView(Sessions sessions, ArlecchinoState state)
    {
        _state = state;
        _sessions = sessions;

        _banner = new(sessions);
    }

    public void Draw(SurfaceRegion frame, Action<SurfaceRegion> body)
    {
        _pane ??= Lay(body);

        _pane.Draw(frame.Inset(new Margin(2, 0, 2, 0)));
    }

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
