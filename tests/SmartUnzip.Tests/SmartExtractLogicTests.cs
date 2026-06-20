namespace SmartUnzip.Tests;

public class SmartExtractLogicTests
{
    [Xunit.Fact]
    public void Determine_SingleMatchingRootFolder_ReturnsDirect()
    {
        var entries = new List<ArchiveEntry>
        {
            new("project", true),
            new("project/readme.txt", false),
            new("project/src", true),
        };
        Xunit.Assert.Equal(ExtractionMode.Direct, SmartExtractLogic.Determine("project", entries));
    }

    [Xunit.Fact]
    public void Determine_CaseInsensitiveMatch_ReturnsDirect()
    {
        var entries = new List<ArchiveEntry>
        {
            new("Project", true),
            new("Project/file.txt", false),
        };
        Xunit.Assert.Equal(ExtractionMode.Direct, SmartExtractLogic.Determine("project", entries));
    }

    [Xunit.Fact]
    public void Determine_MultipleTopLevelEntries_ReturnsWrapped()
    {
        var entries = new List<ArchiveEntry>
        {
            new("readme.txt", false),
            new("src", true),
        };
        Xunit.Assert.Equal(ExtractionMode.Wrapped, SmartExtractLogic.Determine("project", entries));
    }

    [Xunit.Fact]
    public void Determine_SingleRootFolderWithDifferentName_ReturnsWrapped()
    {
        var entries = new List<ArchiveEntry>
        {
            new("old-name", true),
            new("old-name/file.txt", false),
        };
        Xunit.Assert.Equal(ExtractionMode.Wrapped, SmartExtractLogic.Determine("project", entries));
    }

    [Xunit.Fact]
    public void Determine_FilesAtRoot_ReturnsWrapped()
    {
        var entries = new List<ArchiveEntry>
        {
            new("file1.txt", false),
            new("file2.txt", false),
        };
        Xunit.Assert.Equal(ExtractionMode.Wrapped, SmartExtractLogic.Determine("project", entries));
    }

    [Xunit.Fact]
    public void Determine_SingleFileAtRoot_ReturnsWrapped()
    {
        var entries = new List<ArchiveEntry> { new("readme.txt", false) };
        Xunit.Assert.Equal(ExtractionMode.Wrapped, SmartExtractLogic.Determine("readme", entries));
    }

    [Xunit.Fact]
    public void Determine_EmptyEntries_ReturnsWrapped()
    {
        Xunit.Assert.Equal(ExtractionMode.Wrapped,
            SmartExtractLogic.Determine("project", new List<ArchiveEntry>()));
    }

    [Xunit.Fact]
    public void Determine_BackslashSeparatorInSubpaths_TopLevelDetectedCorrectly()
    {
        var entries = new List<ArchiveEntry>
        {
            new("project", true),
            new(@"project\file.txt", false),
        };
        Xunit.Assert.Equal(ExtractionMode.Direct, SmartExtractLogic.Determine("project", entries));
    }
}
