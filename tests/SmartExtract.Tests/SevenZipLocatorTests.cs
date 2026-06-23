namespace SmartExtract.Tests;

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

    [Xunit.Fact]
    public void LocateFromSmartExtractConfig_ReturnsStoredPath_WhenRegistryKeyExists()
    {
        const string testPath = @"C:\Fake\SevenZip\";
        const string regKey = @"Software\SmartExtract";

        // Arrange: write the test value
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regKey);
        key.SetValue("SevenZipPath", testPath);

        try
        {
            // Act
            var result = SevenZipLocator.LocateFromSmartExtractConfig();

            // Assert
            Xunit.Assert.Equal(testPath, result);
        }
        finally
        {
            // Cleanup: remove the test registry key
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(regKey, throwOnMissingSubKey: false);
        }
    }

    [Xunit.Fact]
    public void LocateFromSmartExtractConfig_ReturnsNull_WhenRegistryKeyAbsent()
    {
        // Ensure the key does not exist
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\SmartExtract", throwOnMissingSubKey: false);

        var result = SevenZipLocator.LocateFromSmartExtractConfig();

        Xunit.Assert.Null(result);
    }

    [Xunit.Fact]
    public void Locate_PrefersSmartExtractConfigOverSevenZipRegistry()
    {
        const string testPath = @"C:\Fake\SevenZip\";
        const string regKey = @"Software\SmartExtract";

        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regKey);
        key.SetValue("SevenZipPath", testPath);

        try
        {
            var result = SevenZipLocator.Locate();
            Xunit.Assert.Equal(testPath, result);
        }
        finally
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(regKey, throwOnMissingSubKey: false);
        }
    }
}
