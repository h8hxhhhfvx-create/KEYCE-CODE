using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class DocumentPersistenceServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"KeyceCode-Persistence-{Guid.NewGuid():N}");

    public DocumentPersistenceServiceTests() => Directory.CreateDirectory(root);

    [Fact]
    public void Save_FormatsCSharpAndReturnsWrittenContent()
    {
        var path = Path.Combine(root, "Program.cs");
        var service = new DocumentPersistenceService();

        var result = service.Save(path, "class C{static void Main(){}}", formatCSharp: true, indentationSize: 2);

        Assert.True(result.WasFormatted);
        Assert.Equal(result.Content, File.ReadAllText(path));
        Assert.Contains("class C", result.Content);
    }

    [Fact]
    public void Save_DoesNotFormatNonCSharpFiles()
    {
        var path = Path.Combine(root, "page.html");
        const string content = "<main>hello</main>";
        var result = new DocumentPersistenceService().Save(path, content, formatCSharp: true, indentationSize: 4);

        Assert.False(result.WasFormatted);
        Assert.Equal(content, result.Content);
        Assert.Equal(content, File.ReadAllText(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
