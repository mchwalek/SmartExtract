namespace SmartUnzip.Tests;

public class SevenZipLocatorTests
{
    [Xunit.Fact]
    public void LocateFromRegistry_ReturnsProgramFilesPath()
    {
        var result = SevenZipLocator.LocateFromRegistry();
        Xunit.Assert.NotNull(result);
        Xunit.Assert.Contains("7-Zip", result, System.StringComparison.OrdinalIgnoreCase);
    }

    [Xunit.Fact]
    public void Locate_ReturnedDirectoryContains7zExe()
    {
        var dir = SevenZipLocator.Locate();
        Xunit.Assert.True(System.IO.File.Exists(System.IO.Path.Combine(dir, "7z.exe")),
            $"7z.exe not found in: {dir}");
    }

    [Xunit.Fact]
    public void Locate_ReturnedDirectoryContains7zGExe()
    {
        var dir = SevenZipLocator.Locate();
        Xunit.Assert.True(System.IO.File.Exists(System.IO.Path.Combine(dir, "7zG.exe")),
            $"7zG.exe not found in: {dir}");
    }

    [Xunit.Fact]
    public void Locate_NeverReturnsNull()
    {
        var result = SevenZipLocator.Locate();
        Xunit.Assert.NotNull(result);
        Xunit.Assert.NotEmpty(result);
    }
}
