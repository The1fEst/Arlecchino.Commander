using System;
using System.Threading.Tasks;

namespace Arlecchino.Commander.Model;

/// <summary>
/// Asking something slow and doing something with what comes back. The question goes off on its own and
/// the answer is handed to the drawing thread, which is the only one allowed to change the screen.
/// </summary>
public static class Answers
{
    /// <summary>Asks, and answers on the drawing thread when the answer arrives.</summary>
    /// <typeparam name="T">What was asked for.</typeparam>
    /// <param name="asking">The question.</param>
    /// <param name="answered">What to do with the answer.</param>
    public static void From<T>(Func<Task<T>> asking, Action<T> answered)
    {
        _ = Asked();

        async Task Asked()
        {
            var answer = await asking().ConfigureAwait(false);

            FrameThread.Post(() => answered(answer));
        }
    }
}
