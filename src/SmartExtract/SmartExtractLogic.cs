namespace SmartExtract;

public enum ExtractionMode { Direct, Wrapped }

public static class SmartExtractLogic
{
    public static ExtractionMode Determine(string archiveBaseName, IReadOnlyList<ArchiveEntry> entries)
    {
        var topLevel = entries
            .Where(e => !e.Path.Contains('/') && !e.Path.Contains('\\'))
            .ToList();

        if (topLevel.Count == 1
            && topLevel[0].IsDirectory
            && string.Equals(topLevel[0].Path, archiveBaseName, StringComparison.OrdinalIgnoreCase))
        {
            return ExtractionMode.Direct;
        }

        return ExtractionMode.Wrapped;
    }
}
