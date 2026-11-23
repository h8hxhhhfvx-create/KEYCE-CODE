using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class CompilerServiceTests
{
    [Fact]
    public async Task BuildAsync_BuildsTheCurrentKeyceProject()
    {
        var projectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "KeyceCode.csproj"));
        var service = new CompilerService();

        var result = await service.BuildAsync(projectPath);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Display)));
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.IsError);
    }

    [Fact]
    public async Task RunAsync_ExecutesASingleCSharpFileAndForwardsOutput()
    {
        var directory = Directory.CreateTempSubdirectory("KeyceCodeRunTest");
        try
        {
            var file = Path.Combine(directory.FullName, "Program.cs");
            await File.WriteAllTextAsync(file, "System.Console.WriteLine(\"KEYCE_RUN_OK\");");
            var output = new System.Text.StringBuilder();
            var service = new CompilerService();
            service.OutputReceived += (text, _) => output.Append(text);

            var result = await service.RunAsync(file);

            Assert.True(result.Success, output.ToString());
            Assert.Contains("KEYCE_RUN_OK", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task BuildAsync_InvalidProjectReturnsStructuredDiagnostic()
    {
        var directory = Directory.CreateTempSubdirectory("KeyceCodeDiagnosticTest");
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory.FullName, "Invalid.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            await File.WriteAllTextAsync(
                Path.Combine(directory.FullName, "Program.cs"),
                "class Broken { static void Main() { int value = 1 } }");

            var result = await new CompilerService().BuildAsync(directory.FullName);

            var diagnostic = Assert.Single(result.Diagnostics, item => item.Code == "CS1002");
            Assert.Equal(1, diagnostic.Line);
            Assert.True(diagnostic.Column > 0);
            Assert.True(diagnostic.IsError);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
