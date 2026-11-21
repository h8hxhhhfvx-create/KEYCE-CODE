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
}
