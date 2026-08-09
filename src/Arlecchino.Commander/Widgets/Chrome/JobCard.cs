using System;
using System.Collections.Generic;
using System.Linq;
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
/// Work must never block the panels, and a card says so by sitting beside them: it covers a few rows of the
/// panel it is over and nothing else. Two copies at once are two cards, and a card that has finished stays
/// for as long as the message would have stayed on the output row and then goes. The rule down its left edge
/// is the whole of the report: red while it runs, green when it worked, amber when something did not.
/// </summary>
public sealed class JobCard
{
    private const int CardMinHeight = 3;
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

    /// <summary>
    ///     Draws the stack, or nothing at all when there is nothing to say. The cards are laid from the bottom
    ///     of the region upwards, so the newest one is always in the same corner and the older ones move away
    ///     from it. A card that would reach the top of the panel is not drawn rather than drawn half.
    /// </summary>
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
            var card = Measure(entry, width);

            if (bottom - card.Height < 2)
            {
                return;
            }

            DrawJob(over.Inset(new Margin(over.Width - width - 2, 0, 2, over.Height - bottom)), entry, card);

            bottom -= card.Height + Gap;
        }
    }

    /// <summary>
    /// Which cards to draw, the newest at the bottom. A command that is running is not a notification —
    /// the runner reports it on its own — so it is put at the front of the stack by hand.
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
        var failed = entry.Level is NotificationLevel.Failure or NotificationLevel.Warning;
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

    /// <summary>
    ///     Draws one card, filled from both ends. The row that names a key is the last one, and the bar or the
    ///     reason is the row above it. Both are counted back from the foot, so they stay where the eye left
    ///     them whether the title above took one row or three.
    ///     The title is wrapped and not cut: what a card is usually saying is a path, and the end of a path
    ///     is the part that says which file this is. Three rows is where the wrapping stops, since a title
    ///     longer than that would reach the rows the bar and the hint are holding. <see cref="Measure"/> did
    ///     that wrapping already, since the height it settles on is what the stack needs before any of this.
    /// </summary>
    /// <param name="cardsRegion">Where it goes.</param>
    /// <param name="job">What it says.</param>
    /// <param name="card">How tall it is and how its title sits, worked out beforehand.</param>
    private void DrawJob(SurfaceRegion cardsRegion, Job job, Card card)
    {
        var coat = Skin.Overlay;
        var (height, alignCenter, message) = card;

        var region = cardsRegion.Rows(cardsRegion.Height - height, height);
        var cardContent = region.Inset(new Margin(2, 0, 1, 0));

        region.Fill(coat.Text);
        region.Inset(new Margin(0, 0, region.Width - 1, 0)).Fill(Skin.Paint(job.Rule, job.Rule));

        for (var i = 0; i < message.Length; i++)
        {
            var row = alignCenter ? i + 1 : i;
            cardContent.Write(row, 0, message[i], Said(job, coat));
        }

        if (job.Detail.Length > 0)
        {
            cardContent.Write(height - 3, 0, TextWidth.Truncate(job.Detail, cardContent.Width), coat.Meta);
        }

        if (job.Progress is { } progress)
        {
            var margin = Math.Max(0, (cardContent.Width - BarCells) / 2);
            _bar.Value = (decimal)(progress * 100);
            _bar.Draw(cardContent.Rows(height - 2, 1)
                .Inset(new Margin(margin, 0, Math.Max(0, cardContent.Width - margin - BarCells), 0)));
        }

        if (job.Running)
        {
            cardContent.Write(height - 1, 0, _spinner.Current, coat.Accent);
            cardContent.WriteLine(height - 1, Loc(LocString.SaidStops), coat.Label, Align.Right);
        }
        else if (job.Detail.Length > 0)
        {
            cardContent.WriteLine(height - 1, Loc(LocString.SaidToRead), coat.Label, Align.Right);
        }
    }

    /// <summary>
    ///     Works out how tall a card is before any of it is drawn, which is what lets the stack stop rather
    ///     than lay a card over the top of the panel. A card that has nothing to report beyond its title is
    ///     as tall as that title with a row of air above and below it, and stands centered. Everything else —
    ///     a reason, a bar, the row that names a key — is a row counted back from the foot, so those rows
    ///     stay where the eye left them and the title starts at the top.
    /// </summary>
    /// <param name="job">What the card says.</param>
    /// <param name="width">How wide the card is, which is what the title wraps to.</param>
    /// <returns>The height, whether the title is centered, and the title as its wrapped rows.</returns>
    private static Card Measure(Job job, int width)
    {
        var message = TextWidth.Wrap(job.Title, width - 4).Take(CardMinHeight).ToArray();
        var rows = 0;

        if (job.Detail.Length > 0)
        {
            rows++;
        }

        if (job.Progress != null)
        {
            rows++;
        }

        if (job.Running)
        {
            rows++;
        }

        return rows == 0
            ? new(message.Length + 2, true, message)
            : new(rows + CardMinHeight, false, message);
    }

    /// <summary>What color the line at the top of a card is written in.</summary>
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

    /// <summary>One card: what it says, how far along it is, and the color of its rule.</summary>
    /// <param name="Title">The line at the top.</param>
    /// <param name="Detail">Why it went wrong, when it did.</param>
    /// <param name="Progress">How full its bar is, or nothing when it has none.</param>
    /// <param name="Running">Whether the work is still going.</param>
    /// <param name="Rule">The color down its left edge.</param>
    private sealed record Job(string Title, string Detail, double? Progress, bool Running, Rgb Rule);

    /// <summary>How much room one card takes, and how its title sits in it.</summary>
    /// <param name="Height">How many rows it is.</param>
    /// <param name="AlignCenter">Whether the title stands in the middle rather than starting at the top.</param>
    /// <param name="Message">The title, wrapped to the rows it is drawn on.</param>
    private sealed record Card(int Height, bool AlignCenter, string[] Message);
}
