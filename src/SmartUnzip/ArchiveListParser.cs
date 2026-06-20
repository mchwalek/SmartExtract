namespace SmartUnzip;

public static class ArchiveListParser
{
    public static IReadOnlyList<ArchiveEntry> Parse(string output)
    {
        var entries = new List<ArchiveEntry>();
        var blocks = output.Split("----------", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            string? path = null;
            bool isDirectory = false;
            bool hasFolder = false;

            foreach (var line in block.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("Path = ", StringComparison.Ordinal))
                    path = trimmed["Path = ".Length..].Trim();
                else if (trimmed.StartsWith("Folder = ", StringComparison.Ordinal))
                {
                    isDirectory = trimmed["Folder = ".Length..].Trim() == "+";
                    hasFolder = true;
                }
            }

            if (path != null && hasFolder)
                entries.Add(new ArchiveEntry(path, isDirectory));
        }

        return entries;
    }
}
