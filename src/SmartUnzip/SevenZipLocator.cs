using Microsoft.Win32;

namespace SmartUnzip;

public static class SevenZipLocator
{
    private const string FallbackPath = @"C:\Program Files\7-Zip\";

    public static string Locate() =>
        LocateFromRegistry() ?? LocateFromPath() ?? FallbackPath;

    public static string? LocateFromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\7-Zip");
            if (key is null) return null;
            var path = (key.GetValue("Path64") as string)
                    ?? (key.GetValue("Path") as string);
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        catch { return null; }
    }

    public static string? LocateFromPath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv is null) return null;

        foreach (var dir in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, "7z.exe");
            if (File.Exists(candidate))
            {
                var containing = Path.GetDirectoryName(candidate);
                return containing is null ? null : containing.TrimEnd('\\') + '\\';
            }
        }
        return null;
    }
}
