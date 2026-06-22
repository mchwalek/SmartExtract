namespace SmartExtract.Tests;

public class ArchiveListParserTests
{
    // Format with separate ---------- per entry (common in some archive types / older 7z)
    private const string WellWrappedOutput = """
        7-Zip 26.00 (x64)

        Listing archive: project.zip

        --
        Path = project.zip
        Type = zip

        ----------
        Path = project
        Folder = +
        Size = 0

        ----------
        Path = project/readme.txt
        Folder = -
        Size = 1234

        ----------
        Path = project/src
        Folder = +
        Size = 0
        """;

    // Real-world 7z -slt format: multiple entries in one ---------- block,
    // separated by blank lines (observed with 7-Zip 26.00 on ZIP archives)
    private const string WellWrappedRealFormat = """
        7-Zip 26.00 (x64)

        Listing archive: project.zip

        --
        Path = project.zip
        Type = zip

        ----------
        Path = project
        Folder = +
        Size = 0
        Offset = 0

        Path = project\readme.txt
        Folder = -
        Size = 7
        Offset = 38
        """;

    private const string FlatOutput = """
        7-Zip 26.00 (x64)

        Listing archive: flat.zip

        --
        Path = flat.zip
        Type = zip

        ----------
        Path = readme.txt
        Folder = -
        Size = 100

        ----------
        Path = main.py
        Folder = -
        Size = 200
        """;

    [Xunit.Fact]
    public void Parse_WellWrapped_ReturnsThreeEntries()
    {
        var entries = ArchiveListParser.Parse(WellWrappedOutput);
        Xunit.Assert.Equal(3, entries.Count);
    }

    [Xunit.Fact]
    public void Parse_WellWrapped_ArchiveHeaderNotIncluded()
    {
        var entries = ArchiveListParser.Parse(WellWrappedOutput);
        Xunit.Assert.DoesNotContain(entries, e => e.Path == "project.zip");
    }

    [Xunit.Fact]
    public void Parse_WellWrapped_RootFolderIsDirectory()
    {
        var entries = ArchiveListParser.Parse(WellWrappedOutput);
        var root = Xunit.Assert.Single(entries, e => e.Path == "project");
        Xunit.Assert.True(root.IsDirectory);
    }

    [Xunit.Fact]
    public void Parse_WellWrapped_FileEntryIsNotDirectory()
    {
        var entries = ArchiveListParser.Parse(WellWrappedOutput);
        var file = Xunit.Assert.Single(entries, e => e.Path == "project/readme.txt");
        Xunit.Assert.False(file.IsDirectory);
    }

    [Xunit.Fact]
    public void Parse_RealFormat_MultipleEntriesInOneBlock_ParsedCorrectly()
    {
        var entries = ArchiveListParser.Parse(WellWrappedRealFormat);
        Xunit.Assert.Equal(2, entries.Count);
        var root = Xunit.Assert.Single(entries, e => e.Path == "project");
        Xunit.Assert.True(root.IsDirectory);
        var file = Xunit.Assert.Single(entries, e => e.Path == @"project\readme.txt");
        Xunit.Assert.False(file.IsDirectory);
    }

    [Xunit.Fact]
    public void Parse_Flat_ReturnsTwoEntries()
    {
        var entries = ArchiveListParser.Parse(FlatOutput);
        Xunit.Assert.Equal(2, entries.Count);
        Xunit.Assert.All(entries, e => Xunit.Assert.False(e.IsDirectory));
    }

    [Xunit.Fact]
    public void Parse_EmptyString_ReturnsEmpty()
    {
        var entries = ArchiveListParser.Parse(string.Empty);
        Xunit.Assert.Empty(entries);
    }
}
