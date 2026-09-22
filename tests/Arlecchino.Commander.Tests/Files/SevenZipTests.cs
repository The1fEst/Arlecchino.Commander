using System;
using System.IO;
using Arlecchino.Commander.Files.Archives;
using Xunit;

namespace Arlecchino.Commander.Tests.Files;

/// <summary>
/// Where the program is found when it is not on the path. The folders are handed in, so the search is
/// asserted on any machine rather than only on a Windows one with 7-Zip installed.
/// </summary>
public sealed class SevenZipTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("commander-seven").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    /// <summary>
    /// A copy the installer put in its own folder is found by its whole path, since the bare name
    /// does not run. A folder the registry names that holds no copy is passed over for the next.
    /// </summary>
    [Fact]
    public void ACopyTheInstallerKeptOffThePathIsFound()
    {
        var empty = Directory.CreateDirectory(Path.Combine(_folder, "Tools", "7-Zip")).FullName;
        var holding = Directory.CreateDirectory(Path.Combine(_folder, "Program Files", "7-Zip")).FullName;
        var program = Path.Combine(holding, "7z.exe");

        File.WriteAllText(program, "");

        Assert.Equal(program, SevenZip.InstallerPath([empty, holding]));
    }

    /// <summary>
    /// No copy anywhere is no program at all. A folder Windows could not name comes back empty, and is
    /// not taken for the folder the application was started in.
    /// </summary>
    [Fact]
    public void NoCopyAnywhereIsNoProgram()
    {
        Assert.Equal("", SevenZip.InstallerPath(["", _folder]));
    }
}
