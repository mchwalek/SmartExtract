namespace SmartExtract.Tests;

public class SevenZipLocatorTests
{
    private const string SmartExtractRegistryKey = @"Software\SmartExtract";
    private const string SevenZipPathValue = "SevenZipPath";

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
        var state = CaptureSmartExtractConfigState();

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(SmartExtractRegistryKey);
            key.SetValue(SevenZipPathValue, testPath);

            var result = SevenZipLocator.LocateFromSmartExtractConfig();

            Xunit.Assert.Equal(testPath, result);
        }
        finally
        {
            RestoreSmartExtractConfigState(state);
        }
    }

    [Xunit.Fact]
    public void LocateFromSmartExtractConfig_ReturnsNull_WhenRegistryKeyAbsent()
    {
        var state = CaptureSmartExtractConfigState();

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(SmartExtractRegistryKey, writable: true);
            key?.DeleteValue(SevenZipPathValue, throwOnMissingValue: false);

            var result = SevenZipLocator.LocateFromSmartExtractConfig();

            Xunit.Assert.Null(result);
        }
        finally
        {
            RestoreSmartExtractConfigState(state);
        }
    }

    [Xunit.Fact]
    public void Locate_PrefersSmartExtractConfigOverSevenZipRegistry()
    {
        const string testPath = @"C:\Fake\SevenZip\";
        var state = CaptureSmartExtractConfigState();

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(SmartExtractRegistryKey);
            key.SetValue(SevenZipPathValue, testPath);

            var result = SevenZipLocator.Locate();
            Xunit.Assert.Equal(testPath, result);
        }
        finally
        {
            RestoreSmartExtractConfigState(state);
        }
    }

    private static (bool KeyExists, string? SevenZipPath) CaptureSmartExtractConfigState()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(SmartExtractRegistryKey);
        return (key is not null, key?.GetValue(SevenZipPathValue) as string);
    }

    private static void RestoreSmartExtractConfigState((bool KeyExists, string? SevenZipPath) state)
    {
        if (!state.KeyExists)
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(SmartExtractRegistryKey, throwOnMissingSubKey: false);
            return;
        }

        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(SmartExtractRegistryKey);
        if (state.SevenZipPath is null)
        {
            key.DeleteValue(SevenZipPathValue, throwOnMissingValue: false);
        }
        else
        {
            key.SetValue(SevenZipPathValue, state.SevenZipPath);
        }
    }
}
