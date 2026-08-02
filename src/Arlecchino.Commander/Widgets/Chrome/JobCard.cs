using System;
using Arlecchino.Commander.Stores;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Text;
using Arlecchino.State;
using Arlecchino.Widgets.Readouts;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// What is running, as a card in the corner rather than a band across the screen. Work must never
/// block the panels, and a card says so by sitting beside them: it covers a few rows of the panel it
/// is over and nothing else, and it is gone when the work is.
/// </summary>
public sealed class JobCard
{
    private const int CardWidth = 34;
    private const int BarCells = 22;
    private const int Least = 20;

    private readonly Operations _operations;
    private readonly Runner _runner;
    private readonly ArlecchinoState _state;
    private readonly Spinner _spinner = new();

    private readonly ProgressBar _bar = new()
    {
        Caption = static value => $"{value:0}%",
    };

    /// <summary>Watches the work and says what it is doing.</summary>
    /// <param name="operations">The file work.</param>
    /// <param name="runner">The commands.</param>
    /// <param name="state">Where the last word said is kept.</param>
    public JobCard(Operations operations, Runner runner, ArlecchinoState state)
    {
        _operations = operations;
        _runner = runner;
        _state = state;
    }

    /// <summary>Draws it, or nothing at all when there is nothing to say.</summary>
    /// <param name="over">Everything above the footer, which the card places itself in.</param>
    public void Draw(SurfaceRegion over)
    {
        var running = _operations.IsBusy || _runner.IsRunning;
        var said = _state.Output;

        if (!running && said.Length == 0)
        {
            return;
        }

        var width = Math.Min(CardWidth, over.Width - 4);
        var height = running && _operations.IsMeasured ? 4 : 3;

        if (width < Least || over.Height < height + 2)
        {
            return;
        }

        var card = over
            .Rows(over.Height - height, height)
            .Inset(new Margin(over.Width - width - 2, 0, 2, 0));

        var coat = Skin.Overlay;

        card.Fill(coat.Text);
        card.Rows(0, card.Height).Inset(new Margin(0, 0, card.Width - 1, 0))
            .Fill(Skin.Paint(Skin.Crimson, running ? Skin.Crimson : Skin.AmberRule));

        var inside = card.Inset(new Margin(2, 0, 1, 0));

        if (!running)
        {
            inside.Write(0, 0, TextWidth.Truncate(said, inside.Width), coat.Warning);
            inside.Write(1, 0, Loc(LocString.SaidToRead), coat.Label);

            return;
        }

        _spinner.Advance();

        var title = _operations.IsBusy ? _operations.Progress() : _runner.Last;

        inside.Write(0, 0, TextWidth.Truncate(title, inside.Width), coat.Strong);

        if (_operations.IsMeasured)
        {
            _bar.Value = (decimal)(_operations.Share * 100);
            _bar.Draw(inside.Rows(1, 1).Inset(new Margin(0, 0, Math.Max(0, inside.Width - BarCells), 0)));
        }

        inside.Write(inside.Height - 1, 0, _spinner.Current, coat.Accent);
        inside.WriteLine(inside.Height - 1, Loc(LocString.SaidStops), coat.Label, Align.Right);
    }
}
