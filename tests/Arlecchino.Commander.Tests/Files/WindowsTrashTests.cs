using System;
using System.IO;
using Arlecchino.Commander.Files.Trash;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// The Recycle Bin, which is a shell call and so can only be tested on the machine that has one. What is
/// asserted is that the file left, and that a file never there is answered rather than thrown for.
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
