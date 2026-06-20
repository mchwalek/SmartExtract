using System.Diagnostics;
using System.Text;

namespace SmartUnzip;

public static class ArchiveInspector
{
    public static ExtractionMode Analyze(string sevenZipDir, string archivePath, string archiveBaseName)
    {
        var output = ListArchive(sevenZipDir, archivePath);
        var entries = ArchiveListParser.Parse(output);
        return SmartExtractLogic.Determine(archiveBaseName, entries);
    }

    private static string ListArchive(string sevenZipDir, string archivePath)
    {
        var sevenZip = Path.Combine(sevenZipDir, "7z.exe");
        var psi = new ProcessStartInfo(sevenZip, $"l -slt \"{archivePath}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start: {sevenZip}");

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
