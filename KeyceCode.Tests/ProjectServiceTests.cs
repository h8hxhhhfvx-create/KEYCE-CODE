using KeyceCode.Models;
using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class ProjectServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"KeyceCode-ProjectService-{Guid.NewGuid():N}");
    private readonly ProjectService service = new();

    public ProjectServiceTests() => Directory.CreateDirectory(root);

    [Fact]
    public void OpenProjectFolder_FindsTheSolutionAndSkipsGeneratedFolders()
    {
        var solutionPath = Path.Combine(root, "Sample.sln");
        File.WriteAllText(solutionPath, string.Empty);
        File.WriteAllText(Path.Combine(root, "Sample.csproj"), string.Empty);
        Directory.CreateDirectory(Path.Combine(root, "Models"));
        File.WriteAllText(Path.Combine(root, "Models", "CodeFile.cs"), "class CodeFile { }");
        foreach (var ignored in new[] { "bin", "obj", ".git", ".vs", "node_modules" })
        {
            var ignoredPath = Path.Combine(root, ignored);
            Directory.CreateDirectory(ignoredPath);
            File.WriteAllText(Path.Combine(ignoredPath, "ignored.txt"), "generated");
        }

        var project = service.Open(root);
        var allFiles = Flatten(project.Files).ToArray();

        Assert.Equal(root, project.RootPath);
        Assert.Equal(solutionPath, project.EntryFilePath);
        Assert.Contains(allFiles, file => file.Name == "CodeFile.cs");
        Assert.DoesNotContain(allFiles, file => new[] { "bin", "obj", ".git", ".vs", "node_modules" }.Contains(file.Name));
    }

    [Fact]
    public void OpenProjectFile_UsesTheProjectFileAsItsEntryPoint()
    {
        var projectPath = Path.Combine(root, "Sample.csproj");
        File.WriteAllText(projectPath, string.Empty);

        var project = service.Open(projectPath);

        Assert.Equal(projectPath, project.EntryFilePath);
        Assert.Equal(root, project.RootPath);
    }

    [Fact]
    public void OpenProjectFolder_FindsProjectInsideNestedSourceFolder()
    {
        var nested = Path.Combine(root, "src", "Demo");
        Directory.CreateDirectory(nested);
        var projectPath = Path.Combine(nested, "Demo.csproj");
        File.WriteAllText(projectPath, string.Empty);

        var project = service.Open(root);

        Assert.Equal(projectPath, project.EntryFilePath);
    }

    [Fact]
    public void OpenFolderWithoutCSharpProject_ExplainsTheProblem()
    {
        Assert.Throws<InvalidOperationException>(() => service.Open(root));
    }

    [Fact]
    public async Task CreateCSharpProject_CreatesBuildableAndRunnableStarterFiles()
    {
        var projectPath = service.CreateCSharpProject(root, "Starter");

        Assert.Equal(Path.Combine(root, "Starter", "Starter.csproj"), projectPath);
        Assert.True(File.Exists(projectPath));
        Assert.Contains("Bonjour depuis KEYCE CODE", File.ReadAllText(Path.Combine(root, "Starter", "Program.cs")));
        Assert.Equal(projectPath, service.Open(projectPath).EntryFilePath);

        var compiler = new CompilerService();
        var build = await compiler.BuildAsync(projectPath);
        Assert.True(build.Success, string.Join(Environment.NewLine, build.Diagnostics.Select(d => d.Display)));

        var output = new System.Text.StringBuilder();
        compiler.OutputReceived += (text, _) => output.Append(text);
        var run = await compiler.RunAsync(projectPath);
        Assert.True(run.Success, output.ToString());
        Assert.Contains("Bonjour depuis KEYCE CODE", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CreateCSharpProject_RejectsDuplicateProjectFolder()
    {
        service.CreateCSharpProject(root, "Duplicate");

        Assert.Throws<IOException>(() => service.CreateCSharpProject(root, "Duplicate"));
    }

    [Fact]
    public void OpenMissingPath_ThrowsFileNotFound()
    {
        var missingPath = Path.Combine(root, "missing.csproj");

        Assert.Throws<FileNotFoundException>(() => service.Open(missingPath));
    }

    private static IEnumerable<CodeFile> Flatten(IEnumerable<CodeFile> files) =>
        files.SelectMany(file => new[] { file }.Concat(Flatten(file.Children)));

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
