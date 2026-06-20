namespace SmartUnzip.Tests;

public class ExtractionRunnerTests
{
    [Xunit.Theory]
    [Xunit.InlineData(
        @"C:\downloads\project.zip",
        @"C:\downloads",
        @"x ""C:\downloads\project.zip"" -o""C:\downloads"" -y")]
    [Xunit.InlineData(
        @"C:\downloads\project.zip",
        @"C:\downloads\project",
        @"x ""C:\downloads\project.zip"" -o""C:\downloads\project"" -y")]
    [Xunit.InlineData(
        @"C:\path with spaces\archive.7z",
        @"C:\path with spaces\archive",
        @"x ""C:\path with spaces\archive.7z"" -o""C:\path with spaces\archive"" -y")]
    public void BuildArguments_ReturnsCorrectCommandString(
        string archivePath, string outputDir, string expected)
    {
        Xunit.Assert.Equal(expected, ExtractionRunner.BuildArguments(archivePath, outputDir));
    }
}
