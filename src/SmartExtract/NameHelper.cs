namespace SmartExtract;

public static class NameHelper
{
    private static readonly string[] CompoundSuffixes =
    [
        ".tar.gz", ".tar.bz2", ".tar.xz", ".tar.zst"
    ];

    public static string GetBaseName(string archivePath)
    {
        var fileName = Path.GetFileName(archivePath);
        foreach (var suffix in CompoundSuffixes)
        {
            if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return fileName[..^suffix.Length];
        }
        return Path.GetFileNameWithoutExtension(fileName);
    }
}
