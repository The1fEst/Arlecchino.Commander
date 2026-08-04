using System;
using System.IO;
using Arlecchino.Commander.Files.Trash;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// The Recycle Bin, which is a call into the shell and so can only be held to account on the machine
/// that has one. There is nothing to assert about where the file went — the shell keeps that — so what
/// is asserted is that it left, and that asking about a file that was never there is answered rather
/// than crashed through. The struct the call is handed has to be laid out to the byte, and getting that
/// wrong does not return an error: it takes the process down with it.
/// </summary>
public sealed class WindowsTrashTests
{
    [Fact]
    public void AFileLeavesForTheRecycleBin()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), "commander-trash-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, "what was written");

        try
        {
            Assert.True(WindowsTrash.Instance.TryPut(path));
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void NothingLeavesWhenThereIsNothingToPutAway()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), "commander-trash-never-was-" + Guid.NewGuid().ToString("N") + ".txt");

        Assert.False(WindowsTrash.Instance.TryPut(path));
    }
}
