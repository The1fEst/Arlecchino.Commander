using System;

namespace Arlecchino.Commander.Files.Tty;

/// <summary>
/// A terminal of this application's own making, with a command running at the far end of it believing it
/// is at a real one. How a machine makes one is <see cref="Ttys"/>; what one can do is here.
/// </summary>
public abstract class Tty : IDisposable
{
    /// <summary>Whether the command is still going.</summary>
    public abstract bool IsRunning { get; }

    /// <summary>
    /// Whether this terminal paints itself blank before the command has written a word. A console the
    /// machine makes clears the screen and sends the cursor home; a pair of ends says nothing.
    /// </summary>
    public abstract bool Blanks { get; }

    /// <summary>
    /// Whether this terminal writes back whatever is typed at it. A pair of ends can be told not to,
    /// and a console of the machine's own making cannot be told anything from outside it.
    /// </summary>
    public abstract bool Echoes { get; }

    /// <summary>
    /// What ends a line typed at this terminal. A pair of ends takes the newline; a console takes the
    /// return the Enter key sends, and waits on through a line ended in anything else.
    /// </summary>
    public abstract byte Enter { get; }

    /// <summary>Takes whatever the command has printed, waiting until it prints something.</summary>
    /// <param name="buffer">Where to put it.</param>
    /// <returns>How much was read, and nought or less once there is no more coming.</returns>
    public abstract int Read(byte[] buffer);

    /// <summary>Types at the command, as at a terminal.</summary>
    /// <param name="bytes">What to type.</param>
    /// <param name="count">How much of it.</param>
    /// <returns><c>true</c> when it went.</returns>
    public abstract bool Write(byte[] bytes, int count);

    /// <summary>
    /// Tells the command how large its window is. Saying so wakes whatever is drawing in it, so a window
    /// resized under a program is redrawn at the new size.
    /// </summary>
    /// <param name="columns">How wide.</param>
    /// <param name="rows">How tall.</param>
    public abstract void Resize(int columns, int rows);

    /// <summary>Waits for the command to end and answers with what it ended as.</summary>
    /// <returns>What it exited with.</returns>
    public abstract int Wait();

    /// <summary>Asks the command to end, and everything it started with it.</summary>
    /// <returns><c>true</c> when there was something to ask.</returns>
    public abstract bool Interrupt();

    /// <summary>
    /// Carries the real terminal through to the command and back, for as long as the command has the
    /// screen. It is called on the drawing thread and holds it there, so no frame lands on that screen.
    /// </summary>
    /// <param name="backlog">What the command printed that this application has not passed on.</param>
    /// <param name="count">How much of that there is.</param>
    public abstract void Carry(byte[] backlog, int count);

    /// <summary>Closes the near end, which ends anything still reading from the far one.</summary>
    public abstract void Dispose();
}
