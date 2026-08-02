using System;
using System.Collections.Generic;
using Arlecchino.Commander.Stores;
using Arlecchino.Diagnostics;
using Arlecchino.Rendering;
using Arlecchino.Rendering.Colors;
using Arlecchino.Rendering.Text;
using Arlecchino.State;
using Arlecchino.Widgets.Readouts;

namespace Arlecchino.Commander.Widgets.Chrome;

/// <summary>
/// What is running, as a stack of cards in the corner rather than a band across the screen.
///
/// Work must never block the panels, and a card says so by sitting beside them: it covers a few rows
/// of the panel it is over and nothing else. Two copies at once are two cards, and a card that has
/// finished stays for as long as the message would have stayed on the output row and then goes — the
/// rule down its left edge is the whole of the report, red while it runs, green when it worked, amber
/// when something did not.
/// </summary>
public sealed class JobCard
{
    private const int CardWidth = 34;
    private const int BarCells = 22;
    private const int Least = 20;
    private const int MostCards = 3;
    private const int Gap = 1;

    private readonly Runner _runner;
    private readonly ArlecchinoState _state;
    private readonly Spinner _spinner = new();

    private readonly ProgressBar _bar = new()
    {
        Caption = static value => $"{value:0}%",
    };

    /// <summary>Watches the work and says what it is doing.</summary>
    /// <param name="runner">The commands, which report themselves rather than through a notification.</param>
    /// <param name="state">Where everything the application has said lately is kept.</param>
    public JobCard(Runner runner, ArlecchinoState state)
    {
        _runner = runner;
        _state = state;
    }

    /// <summary>Draws the stack, or nothing at all when there is nothing to say.</summary>
    /// <param name="over">Everything above the footer, which the cards place themselves in.</param>
    public void Draw(SurfaceRegion over)
    {
        var width = Math.Min(CardWidth, over.Width - 4);

        if (width < Least)
        {
            return;
        }

        var showing = Showing();
        var bottom = over.Height;

        _spinner.Advance();

        foreach (var entry in showing)
        {
            var height = Height(entry);

            if (bottom - height < 2)
            {
                return;
            }

            bottom -= height;

            One(over.Rows(bottom, height).Inset(new Margin(over.Width - width - 2, 0, 2, 0)), entry);

            bottom -= Gap;
        }
    }

    /// <summary>
    /// Which cards to draw, newest at the bottom. A command that is running is not a notification —
    /// the runner reports it itself — so it is put at the front of the stack by hand.
    /// </summary>
    /// <returns>The cards, in the order they are drawn from the bottom up.</returns>
    private List<Job> Showing()
    {
        var showing = new List<Job>(MostCards);

        if (_runner.IsRunning)
        {
            showing.Add(new(_runner.Last, "", null, true, Skin.Crimson));
        }

        foreach (var entry in _state.Notifications.Recent)
        {
            if (showing.Count == MostCards)
            {
                break;
            }

            showing.Add(Of(entry));
        }

        return showing;
    }

    /// <summary>What one notification looks like as a card.</summary>
    /// <param name="entry">The notification.</param>
    /// <returns>The card.</returns>
    private static Job Of(Notification entry)
    {
        var failed = entry.Loudness is NotificationLevel.Failure or NotificationLevel.Warning;
        var detail = entry.IsRunning || !failed ? "" : entry.Whole();

        return new(
            entry.Line,
            Ends(detail),
            entry.Filled(),
            entry.IsRunning,
            entry.IsRunning ? Skin.Crimson : failed ? Skin.AmberRule : Skin.Calm);
    }

    /// <summary>The reason a job gives for having gone wrong, cut to the first line of it.</summary>
    /// <param name="detail">Everything it had to say.</param>
    /// <returns>The first line, or nothing when there was none.</returns>
    private static string Ends(string detail)
    {
        var end = detail.IndexOfAny(['\r', '\n']);

        return end < 0 ? detail : detail[..end];
    }

    /// <summary>How many rows a card takes: what it says, what it is doing, and the hint.</summary>
    /// <param name="job">The card.</param>
    /// <returns>The rows.</returns>
    private static int Height(Job job) => job.Share is null && job.Detail.Length == 0 ? 2 : 3;

    /// <summary>Draws one card.</summary>
    /// <param name="card">Where it goes.</param>
    /// <param name="job">What it says.</param>
    private void One(SurfaceRegion card, Job job)
    {
        var coat = Skin.Overlay;

        card.Fill(coat.Text);
        card.Rows(0, card.Height).Inset(new Margin(0, 0, card.Width - 1, 0))
            .Fill(Skin.Paint(job.Rule, job.Rule));

        var inside = card.Inset(new Margin(2, 0, 1, 0));

        inside.Write(0, 0, TextWidth.Truncate(job.Title, inside.Width), Said(job, coat));

        if (job.Share is { } share)
        {
            _bar.Value = (decimal)(share * 100);
            _bar.Draw(inside.Rows(1, 1).Inset(new Margin(0, 0, Math.Max(0, inside.Width - BarCells), 0)));
        }
        else if (job.Detail.Length > 0)
        {
            inside.Write(1, 0, TextWidth.Truncate(job.Detail, inside.Width), coat.Meta);
        }

        if (job.Running)
        {
            inside.Write(inside.Height - 1, 0, _spinner.Current, coat.Accent);
            inside.WriteLine(inside.Height - 1, Loc(LocString.SaidStops), coat.Label, Align.Right);

            return;
        }

        if (job.Detail.Length > 0)
        {
            inside.WriteLine(inside.Height - 1, Loc(LocString.SaidToRead), coat.Label, Align.Right);
        }
    }

    /// <summary>What colour the line at the top of a card is written in.</summary>
    /// <param name="job">The card.</param>
    /// <param name="coat">The surface it is on.</param>
    /// <returns>The style.</returns>
    private static TermColor Said(Job job, Skin.Coat coat)
    {
        if (job.Running)
        {
            return coat.Strong;
        }

        return job.Rule.Equals(Skin.AmberRule) ? coat.Warning : coat.Done;
    }

    /// <summary>One card: what it says, how far along it is, and the colour of its rule.</summary>
    /// <param name="Title">The line at the top.</param>
    /// <param name="Detail">Why it went wrong, when it did.</param>
    /// <param name="Share">How full its bar is, or nothing when it has none.</param>
    /// <param name="Running">Whether the work is still going.</param>
    /// <param name="Rule">The colour down its left edge.</param>
    private sealed record Job(string Title, string Detail, double? Share, bool Running, Rgb Rule);
}
