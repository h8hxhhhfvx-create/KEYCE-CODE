using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class SearchServiceTests
{
    [Fact]
    public void Search_ReturnsFileLineAndColumn()
    {
        var root = Path.Combine(Path.GetTempPath(), "KeyceCodeSearch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "Demo.cs");
            File.WriteAllText(file, "class Demo\n{\n    static void Main() { Console.WriteLine(\"KEYCE\"); }\n}");

            var result = Assert.Single(new SearchService().Search(root, "KEYCE"));

            Assert.Equal(file, result.FilePath);
            Assert.Equal(3, result.Line);
            Assert.True(result.Column > 0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Search_IgnoresBuildDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "KeyceCodeSearch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "bin"));
        try
        {
            File.WriteAllText(Path.Combine(root, "bin", "Generated.cs"), "SEARCH_ME");

            Assert.Empty(new SearchService().Search(root, "SEARCH_ME"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReplaceAll_ReplacesEveryOccurrenceAndReportsCount()
    {
        var root = Path.Combine(Path.GetTempPath(), "KeyceCodeSearch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "Demo.cs");
            File.WriteAllText(file, "one ONE two");
            var result = Assert.Single(new SearchService().ReplaceAll(root, "one", "X"));
            Assert.Equal(2, result.Occurrences);
            Assert.Equal("X X two", File.ReadAllText(file));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
