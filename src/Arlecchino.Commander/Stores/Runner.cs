using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Arlecchino.Atoms;
using Arlecchino.Atoms.Local;
using Arlecchino.Commander.Files.Sources;
using Arlecchino.Commander.Files.Ssh;
using Arlecchino.Commander.Widgets.Dialogs;
using Arlecchino.Hosting;
using Arlecchino.State;

namespace Arlecchino.Commander.Stores;

/// <summary>
/// What the command line runs, what came back, and what is said to it while it runs. It lives outside
/// the screen, so a command outlasts the walk to the output and back and a question follows the user.
/// </summary>
public sealed class Runner : IArlecchinoStore
{
    private const int MostLines = 2000;

    /// <summary>
    /// How many dots a secret is written into the roll as. It is the same count whatever was typed, since
    /// a row of dots as long as the password says more about it than nothing at all would.
    /// </summary>
    private const int Dots = 8;

    private readonly ArlecchinoState _state;
    private readonly Handover _handover;
    private readonly Dialogs _dialogs;
    private readonly ConcurrentQueue<string> _pending = new();

    private IShellRun? _running;
    private int _draining;

    /// <summary>Gathers what running a command needs.</summary>
    /// <param name="state">Where the last word said is kept.</param>
    /// <param name="handover">What lends the terminal to a command that has asked for the screen.</param>
    public Runner(ArlecchinoState state, Handover handover)
    {
        _state = state;
        _handover = handover;
        _dialogs = new(state);
    }

    /// <summary>
    /// What the commands have said, the oldest first, trimmed to the newest two thousand lines. A list
    /// atom, so output landing on the drawing thread marks the frame stale by itself.
    /// </summary>
    public LocalAtomsList<string> Lines { get; } = new();

    public List<string> History { get; } = [];

    public string Last { get; private set; } = "";

    public bool IsRunning { get; private set; }

    /// <summary>
    /// What the running command has asked and is waiting to be told, or nothing when it is waiting for
    /// nothing. The line under the panels says it, and the dialog that opened over it asks it.
    /// </summary>
    public string Asking { get; private set; } = "";

    /// <summary>Whether there is a question standing, which is what makes the line hide what is typed.</summary>
    public bool IsAsking => Asking.Length > 0;

    /// <summary>
    /// Runs a command where the panel is looking — on this machine for a local panel, on the server
    /// over the connection the panel already holds for a remote one.
    /// </summary>
    /// <param name="command">What was typed.</param>
    /// <param name="folder">The folder to run it in.</param>
    /// <param name="source">The panel's source, which decides where it runs.</param>
    /// <param name="finished">Called on the drawing thread once it has ended.</param>
    public void Run(string command, string folder, IFileSource source, Action finished)
    {
        if (IsRunning)
        {
            _state.Output = Loc(LocString.SaidCommandRunning);
            return;
        }

        Last = command;
        IsRunning = true;
        Asking = "";

        Remember(command);
        Lines.Add($"$ {command}");

        FrameThread.Post(async () =>
        {
            await SayAsync(source, command, folder);

            IsRunning = false;
            Asking = "";
            _running = null;

            Drain();

            _state.Output = Loc(LocString.SaidCommandDone, command);

            finished();
        });
    }

    /// <summary>
    /// Answers whatever the running command asked. The answer comes from the dialog and nowhere else,
    /// and is never written down: it goes into the roll as dots.
    /// </summary>
    /// <param name="text">What to send it.</param>
    public void Answer(string text)
    {
        if (_running is not { Listens: true } run)
        {
            _state.Output = Loc(LocString.SaidNothingRunning);
            return;
        }

        if (!run.Say(text))
        {
            _state.Output = Loc(LocString.SaidNothingHeard);
            return;
        }

        Drain();

        Asking = "";

        Lines.Add($"> {new string('•', Dots)}");
        Trim();

        _state.Output = Loc(LocString.SaidAnswerSent);
    }

    /// <summary>
    /// Says there is no more to be typed, which is what <c>Ctrl+D</c> does at a terminal. A command
    /// waiting on its input for a file that is never coming ends here rather than waiting for good.
    /// </summary>
    public void EndInput()
    {
        if (_running is not { } run || !run.EndInput())
        {
            _state.Output = Loc(LocString.SaidNothingRunning);
            return;
        }

        Asking = "";
        _state.Output = Loc(LocString.SaidInputEnded);
    }

    /// <summary>
    /// Puts the standing question back on screen, for when the dialog was closed and the answer is wanted
    /// after all. Nothing is asked when the command is not waiting on anything.
    /// </summary>
    public void AskAgain()
    {
        if (IsAsking)
        {
            Ask(Asking);
        }
    }

    /// <summary>Kills what is running, along with anything it started.</summary>
    public void Stop()
    {
        var running = _running;

        if (running is null)
        {
            _state.Output = Loc(LocString.SaidNothingRunning);
            return;
        }

        var failure = running.Interrupt();

        _state.Output = failure.Length == 0 ? Loc(LocString.SaidStopped) : failure;
    }

    public void Clear() => Lines.Clear();

    private void Remember(string command)
    {
        History.Remove(command);
        History.Add(command);
    }

    /// <summary>
    /// Starts the command wherever the panel is looking and reads it to its end. Which of them that
    /// is, the source answers; this knows only that a run can be held on to, talked to and stopped.
    /// </summary>
    /// <param name="source">The panel's source.</param>
    /// <param name="command">The command as it is run.</param>
    /// <param name="folder">The folder to run it in.</param>
    private async Task SayAsync(IFileSource source, string command, string folder)
    {
        if (source.Start(command, folder) is not { } run)
        {
            Lines.Add($"[failed] {Loc(LocString.SaidRunsNoCommands, source.Label)}");

            return;
        }

        _running = run;

        using (run)
        {
            await run.ReadAsync(new(Prints, Asks, Lends), CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Steps the screen aside for a command that has claimed it, and waits until it has ended. Lending is
    /// the drawing thread's to do, and the command is read on another, so the work is handed over.
    /// </summary>
    /// <param name="work">Carrying the terminal through, which lasts as long as the command does.</param>
    /// <returns>A task that ends once the terminal is ours again.</returns>
    private Task Lends(Action work)
    {
        var loan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        FrameThread.Post(() =>
        {
            try
            {
                _handover.Give(work);
                loan.SetResult();
            }
            catch (InvalidOperationException failure)
            {
                Lines.Add($"[failed] {failure.Message}");
                loan.SetResult();
            }
        });

        return loan.Task;
    }

    /// <summary>
    /// Takes a line the command printed, from whichever thread was reading it. Lines are queued rather
    /// than posted one by one, so a command printing thousands of them asks for one frame and not thousands.
    /// </summary>
    /// <param name="line">The line.</param>
    private void Prints(string line)
    {
        _pending.Enqueue(line);

        if (Interlocked.Exchange(ref _draining, 1) == 0)
        {
            FrameThread.Post(Drain);
        }
    }

    /// <summary>Takes a question the command stopped on, from whichever thread was reading it.</summary>
    /// <param name="prompt">What it asked.</param>
    private void Asks(string prompt) => FrameThread.Post(() =>
    {
        Drain();

        Asking = prompt;
        _state.Output = prompt;

        Ask(prompt);
    });

    /// <summary>
    /// Puts the question to the user, in the dialog everything else is asked in. What is typed is hidden,
    /// since a line a command stopped mid-way on is a password far more often than it is anything else.
    /// </summary>
    /// <param name="prompt">What the command asked.</param>
    private void Ask(string prompt)
    {
        if (_state.Modal is not null)
        {
            return;
        }

        _dialogs.AskFor(
            Loc(LocString.AskingTitle, Named(Last)),
            prompt,
            "",
            Loc(LocString.AskingVerb),
            Answer,
            Loc(LocString.AskingHint),
            secret: true);
    }

    /// <summary>
    /// What to call the command in the title of the question, which is the word it starts with. A line
    /// long enough to have stopped and asked something is too long to stand as a title.
    /// </summary>
    /// <param name="command">What was typed.</param>
    /// <returns>The name.</returns>
    private static string Named(string command)
    {
        var end = command.IndexOf(' ', StringComparison.Ordinal);

        return end < 0 ? command : command[..end];
    }

    /// <summary>
    /// Writes out what the command has printed since the last frame. A line arriving is the end of the
    /// line the question was read off, so whatever was being asked is no longer being asked.
    /// </summary>
    private void Drain()
    {
        Volatile.Write(ref _draining, 0);

        while (_pending.TryDequeue(out var line))
        {
            Lines.Add(line);
            Asking = "";
        }

        Trim();
    }

    private void Trim()
    {
        if (Lines.Count > MostLines)
        {
            Lines.RemoveRange(0, Lines.Count - MostLines);
        }
    }
}
