using System;
using System.Threading.Tasks;

namespace Arlecchino.Commander.Files.Ssh;

/// <summary>
/// The three things a run has to say while it goes: a line, a question, and the moment it asks for the
/// screen. All of them arrive on the reading thread, so whoever takes them owes the frame a post.
/// </summary>
/// <param name="Prints">Takes a line, as it arrives.</param>
/// <param name="Asks">Takes a line the run wrote and did not finish, which is a question.</param>
/// <param name="Lends">
/// Lends the terminal to the run, for the length of the work handed to it. It answers with a task that
/// ends once the terminal has been taken back, which is the only point at which drawing may begin again.
/// </param>
public sealed record ShellTalk(Action<string> Prints, Action<string> Asks, Func<Action, Task> Lends);
