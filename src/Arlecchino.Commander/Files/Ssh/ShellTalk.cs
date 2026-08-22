using System;

namespace Arlecchino.Commander.Files.Ssh;

/// <summary>
/// The two things a run has to say while it is going: a line it printed, and a question it stopped on.
/// Both arrive on whichever thread was reading, so whoever takes them owes the drawing thread a post.
/// </summary>
/// <param name="Prints">Takes a line, as it arrives.</param>
/// <param name="Asks">Takes a line the run wrote and did not finish, which is a question.</param>
public sealed record ShellTalk(Action<string> Prints, Action<string> Asks);
