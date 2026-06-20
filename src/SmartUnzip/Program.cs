using System.Windows.Forms;
using SmartUnzip;

class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            MessageBox.Show("Usage: SmartUnzip <archive-path>",
                "SmartUnzip", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        var archivePath = args[0];

        if (!File.Exists(archivePath))
        {
            MessageBox.Show($"File not found:\n{archivePath}",
                "SmartUnzip", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        try
        {
            var sevenZipDir = SevenZipLocator.Locate();

            if (!File.Exists(Path.Combine(sevenZipDir, "7z.exe"))
             || !File.Exists(Path.Combine(sevenZipDir, "7zG.exe")))
            {
                MessageBox.Show(
                    $"7-Zip executables not found in:\n{sevenZipDir}\n\nPlease install 7-Zip.",
                    "SmartUnzip", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            var baseName  = NameHelper.GetBaseName(archivePath);
            var parentDir = Path.GetDirectoryName(archivePath)
                         ?? Directory.GetCurrentDirectory();
            var mode      = ArchiveInspector.Analyze(sevenZipDir, archivePath, baseName);
            var outputDir = mode == ExtractionMode.Direct
                          ? parentDir
                          : Path.Combine(parentDir, baseName);

            ExtractionRunner.Extract(sevenZipDir, archivePath, outputDir);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Extraction failed:\n{ex.Message}",
                "SmartUnzip", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
