using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Atoms;
using Arlecchino.Atoms.Local;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Files.Work;
using Arlecchino.Commander.Model;
using Arlecchino.Diagnostics;
using Arlecchino.State;

namespace Arlecchino.Commander.Stores;

/// <summary>
/// The copy, move and delete that are running, kept outside any one screen. Work started from the
/// panels outlives them — opening the notifications and coming back builds the commander again — so a
/// store holds it, and the screen reads what is running rather than owning it.
/// </summary>
public sealed class Operations : IArlecchinoStore
{
    private const int RedrawInterval = 120;

    private readonly ArlecchinoState _state;

    private CancellationTokenSource? _cancelling;
    private Outcome? _running;
    private Notification? _reporting;
    private string _busy = "";

    public Operations(ArlecchinoState state) => _state = state;

    /// <summary>Bumped whenever work finishes, so a screen knows its panels are out of date.</summary>
    public Atom<int> Revision { get; } = new LocalAtom<int>(0);

    /// <summary>Whether a copy, move or delete is still running in the background.</summary>
    public bool IsBusy => _running is not null;

    /// <summary>Whether the work has been sized up, which is when a bar for it means anything.</summary>
    public bool IsMeasured => _running?.IsMeasured ?? false;

    /// <summary>How full that bar should be, from <c>0</c> to <c>1</c>.</summary>
    public double Share => _running?.Share ?? 0;

    /// <summary>What to show while the work runs: what it is doing and how far it has got.</summary>
    /// <returns>The line, or an empty string when nothing is running.</returns>
    public string Progress() => _running is { } outcome ? $"{_busy} {outcome.Progress()}" : "";

    /// <summary>Asks the running operation to stop; what it already did stays done.</summary>
    public void Cancel() => _cancelling?.Cancel();

    public void Copy(IFileSource from, IReadOnlyList<FileEntry> sources, IFileSource to, string target) =>
        Start(
            (outcome, token) => FileTasks.CopyAsync(from, sources, to, target, outcome, token),
            Loc(LocString.WorkCopied),
            Loc(LocString.WorkCopying),
            Sizing(from, sources));

    public void Move(IFileSource from, IReadOnlyList<FileEntry> sources, IFileSource to, string target) =>
        Start(
            (outcome, token) => MovingAsync(from, sources, to, target, outcome, token),
            Loc(LocString.WorkMoved),
            Loc(LocString.WorkMoving),
            Sizing(from, sources));

    public void Rename(IFileSource source, FileEntry entry, string target) =>
        Start(
            (outcome, _) => FileTasks.RenameAsync(source, entry, target, outcome),
            Loc(LocString.WorkRenamed),
            Loc(LocString.WorkRenaming));

    /// <summary>
    /// Gets rid of what was chosen. Putting something in the trash is a rename and nothing more, so it
    /// is not counted first — the counting pass exists to fill a progress bar that a rename outruns.
    /// </summary>
    /// <param name="source">Where the entries live.</param>
    /// <param name="entries">What to get rid of.</param>
    /// <param name="toTrash">Whether to put it where it could be fetched back from.</param>
    public void Delete(IFileSource source, IReadOnlyList<FileEntry> entries, bool toTrash = false) =>
        Start(
            (outcome, token) => FileTasks.DeleteAsync(source, entries, toTrash, outcome, token),
            Loc(toTrash ? LocString.WorkTrashed : LocString.WorkDeleted),
            Loc(toTrash ? LocString.WorkTrashing : LocString.WorkDeleting),
            toTrash ? null : Sizing(source, entries));

    private static async Task MovingAsync(
        IFileSource from,
        IReadOnlyList<FileEntry> sources,
        IFileSource to,
        string target,
        Outcome outcome,
        CancellationToken token)
    {
        if (sources.Count == 1 && !await to.FolderExistsAsync(target, token).ConfigureAwait(false))
        {
            if (ReferenceEquals(from, to))
            {
                await FileTasks.RenameAsync(from, sources[0], target, outcome).ConfigureAwait(false);

                return;
            }

            await FileTasks.MoveAsync(from, sources, to, to.Parent(target) ?? target, outcome, token)
                .ConfigureAwait(false);

            return;
        }

        await FileTasks.MoveAsync(from, sources, to, target, outcome, token).ConfigureAwait(false);
    }

    /// <summary>
    /// The counting pass, or nothing when counting would cost more than the work. Walking a tree on a
    /// server is a request per folder, and a deletion there is one command anyway, so the bar is worth
    /// less than the wait it would add.
    /// </summary>
    /// <param name="source">Where the entries live.</param>
    /// <param name="entries">What is about to be worked on.</param>
    /// <returns>How to count, or <c>null</c> to skip counting.</returns>
    private static Func<CancellationToken, Task<Tally>>? Sizing(
        IFileSource source,
        IReadOnlyList<FileEntry> entries) =>
        source.WalksCheaply ? token => FileTasks.MeasureAsync(source, entries, token) : null;

    /// <summary>
    /// Runs the work away from the drawing thread, even on a local disk, where a folder deep enough would
    /// otherwise freeze the frame. It reports itself as a notification that counts up while it goes and
    /// turns into what came of it at the end. Nothing is occupied while the disk or the server is thinking;
    /// what comes back is posted to the frame, the only thread allowed to change the screen.
    /// </summary>
    private void Start(
        Func<Outcome, CancellationToken, Task> work,
        string verb,
        string busy,
        Func<CancellationToken, Task<Tally>>? measure = null)
    {
        if (IsBusy)
        {
            _state.Output = Loc(LocString.SaidBusyStops, _busy);
            return;
        }

        var outcome = new Outcome();
        var cancelling = new CancellationTokenSource();

        _running = outcome;
        _busy = busy;
        _cancelling = cancelling;
        _reporting = _state.Notifications.Raise(new(DateTimeOffset.Now, NotificationLevel.Information, busy)
        {
            Progress = Progress,
            Share = () => outcome.IsMeasured ? outcome.Share : null,
            Detail = Progress,
            Actions = [new(static () => Loc(LocString.WorkStop), Cancel)],
        });

        Redraw(cancelling.Token);

        _ = Working();

        async Task Working()
        {
            try
            {
                outcome.Planning(measure is null
                    ? default
                    : await measure(cancelling.Token).ConfigureAwait(false));

                await work(outcome, cancelling.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            finally
            {
                var stopped = cancelling.Token.IsCancellationRequested;

                FrameThread.Post(() => Finish(outcome, verb, stopped));
            }
        }
    }

    private void Redraw(CancellationToken token) => Task.Run(
        async () =>
        {
            while (!token.IsCancellationRequested && IsBusy)
            {
                await Task.Delay(RedrawInterval, CancellationToken.None);

                FrameThread.Post(_state.Invalidate);
            }
        },
        CancellationToken.None);

    private void Finish(Outcome outcome, string verb, bool stopped)
    {
        var errors = outcome.Errors;
        var said = stopped ? $"{outcome.Describe(verb)} — stopped" : outcome.Describe(verb);

        _running = null;
        _cancelling?.Dispose();
        _cancelling = null;

        if (_reporting is { } reporting)
        {
            reporting.Detail = errors.Count > 0 ? () => string.Join(Environment.NewLine, errors) : null;

            _state.Notifications.Settle(
                reporting,
                said,
                errors.Count > 0 ? NotificationLevel.Failure : NotificationLevel.Information);

            _reporting = null;
        }

        Revision.Value++;
    }
}
