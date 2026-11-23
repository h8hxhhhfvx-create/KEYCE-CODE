using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class TestServiceTests
{
    [Fact]
    public void ResolveTestProject_FindsProjectInDirectory()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var project = new TestService().ResolveTestProject(Path.Combine(root, "KeyceCode.Tests"));
        Assert.EndsWith("KeyceCode.Tests.csproj", project, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveTestProject_PrefersSolutionInDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("KeyceCodeTestSolution");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "Sample.slnx"),
                "<Solution><Project Path=\"Nested\\Sample.csproj\" /></Solution>");
            Directory.CreateDirectory(Path.Combine(directory.FullName, "Nested"));
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "Nested", "Sample.csproj"), "<Project />");

            var project = new TestService().ResolveTestProject(directory.FullName);

            Assert.EndsWith("Sample.slnx", project, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
