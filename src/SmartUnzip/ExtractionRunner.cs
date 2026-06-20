using System.Diagnostics;

namespace SmartUnzip;

public static class ExtractionRunner
{
    public static string BuildArguments(string archivePath, string outputDir) =>
        $"x \"{archivePath}\" -o\"{outputDir}\" -y";

    public static void Extract(string sevenZipDir, string archivePath, string outputDir)
    {
        var sevenZipGui = Path.Combine(sevenZipDir, "7zG.exe");
        // UseShellExecute = true: 7zG.exe is a GUI application that requires
        // proper shell/window station initialization to run correctly.
        var psi = new ProcessStartInfo(sevenZipGui, BuildArguments(archivePath, outputDir))
        {
            UseShellExecute = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start: {sevenZipGui}");

        process.WaitForExit();
    }
}
