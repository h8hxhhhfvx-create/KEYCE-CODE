using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class FileServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"KeyceCode-FileService-{Guid.NewGuid():N}");
    private readonly FileService service = new();

    public FileServiceTests() => Directory.CreateDirectory(root);

    [Fact]
    public void WriteText_CreatesParentFoldersAndPreservesContent()
    {
        var path = Path.Combine(root, "src", "Nested", "Program.cs");
        const string content = "// été\nConsole.WriteLine(\"Bonjour\");";

        service.WriteText(path, content);

        Assert.True(File.Exists(path));
        Assert.Equal(content, service.ReadText(path));
    }

    [Fact]
    public void CreateFile_ReturnsAnAbsolutePathToAnEmptyFile()
    {
        var file = service.CreateFile(root, "Program.cs");

        Assert.True(Path.IsPathFullyQualified(file.FullPath));
        Assert.Equal("Program.cs", file.Name);
        Assert.True(File.Exists(file.FullPath));
        Assert.Equal(string.Empty, File.ReadAllText(file.FullPath));
    }

    [Fact]
    public void CreateFile_RejectsANameContainingAPath()
    {
        Assert.Throws<ArgumentException>(() => service.CreateFile(root, Path.Combine("outside", "Program.cs")));
    }

    [Fact]
    public void CreateFile_DoesNotOverwriteAnExistingFile()
    {
        var path = Path.Combine(root, "Program.cs");
        File.WriteAllText(path, "contenu à conserver");

        Assert.Throws<IOException>(() => service.CreateFile(root, "Program.cs"));
        Assert.Equal("contenu à conserver", File.ReadAllText(path));
    }

    [Fact]
    public void Rename_ChangesTheFileNameAndKeepsItsContent()
    {
        var original = Path.Combine(root, "Old.cs");
        File.WriteAllText(original, "class Example { }");

        var renamed = service.Rename(original, "New.cs");

        Assert.False(File.Exists(original));
        Assert.Equal("New.cs", Path.GetFileName(renamed));
        Assert.Equal("class Example { }", File.ReadAllText(renamed));
    }

    [Fact]
    public void Delete_RemovesAFile()
    {
        var path = Path.Combine(root, "DeleteMe.cs");
        File.WriteAllText(path, "temporary");

        service.Delete(path);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void CreateDirectory_RejectsAnExistingName()
    {
        service.CreateDirectory(root, "Models");

        Assert.Throws<IOException>(() => service.CreateDirectory(root, "Models"));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
