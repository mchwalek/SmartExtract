namespace SmartExtract.Tests;

public class NameHelperTests
{
    [Xunit.Theory]
    [Xunit.InlineData(@"C:\downloads\project.zip", "project")]
    [Xunit.InlineData(@"C:\downloads\project.7z", "project")]
    [Xunit.InlineData(@"C:\downloads\project.rar", "project")]
    [Xunit.InlineData(@"C:\downloads\project.tar", "project")]
    [Xunit.InlineData(@"C:\downloads\project.gz", "project")]
    [Xunit.InlineData(@"C:\downloads\project.bz2", "project")]
    [Xunit.InlineData(@"C:\downloads\project.tar.gz", "project")]
    [Xunit.InlineData(@"C:\downloads\project.tar.bz2", "project")]
    [Xunit.InlineData(@"C:\downloads\project.tar.xz", "project")]
    [Xunit.InlineData(@"C:\downloads\project.tar.zst", "project")]
    [Xunit.InlineData(@"C:\downloads\my-project.tar.gz", "my-project")]
    [Xunit.InlineData(@"C:\downloads\My Project.zip", "My Project")]
    [Xunit.InlineData(@"C:\downloads\archive.TAR.GZ", "archive")]
    public void GetBaseName_ReturnsCorrectBaseName(string path, string expected)
    {
        Xunit.Assert.Equal(expected, NameHelper.GetBaseName(path));
    }
}
