namespace SmartUnzip;

public static class ArchiveListParser
{
    /// <summary>
    /// Parses the stdout of "7z.exe l -slt" into a list of archive entries.
    /// 
    /// 7z -slt output format:
    ///   - The archive metadata block comes before the first "----------" separator
    ///   - Entry blocks are delimited by "----------" (ten dashes), but multiple
    ///     entries may appear within one "----------" section, separated by blank lines
    ///   - Only emits an entry when both Path and Folder lines are present
    ///     (this filters the archive-level metadata block which has Path but no Folder)
    /// </summary>
    public static IReadOnlyList<ArchiveEntry> Parse(string output)
    {
        var entries = new List<ArchiveEntry>();

        // Split into major sections on "----------" (ten dashes)
        var sections = output.Split("----------", StringSplitOptions.RemoveEmptyEntries);

        foreach (var section in sections)
        {
            // Within each section, individual entries are separated by blank lines.
            // This handles both single-entry-per-section and multi-entry-per-section formats.
            var entryBlocks = section.Split(
                new[] { "\r\n\r\n", "\n\n" },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (var block in entryBlocks)
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

                // Only add when both Path and Folder are present.
                // The archive metadata block has Path but no Folder.
                if (path != null && hasFolder)
                    entries.Add(new ArchiveEntry(path, isDirectory));
            }
        }

        return entries;
    }
}
