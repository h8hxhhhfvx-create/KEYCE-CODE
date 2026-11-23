using System.Text;
using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

[Collection("Process integration")]
public sealed class LanguageRunnerIntegrationTests
{
    private readonly LanguageRunnerService languageRunner = new();
    private readonly ToolDetectionService tools = new();

    [Fact]
    public async Task PythonScriptRunsThroughTheLanguageRunner()
    {
        await AssertScriptRuns("python", ".py", "print('KEYCE_PYTHON_OK')", "KEYCE_PYTHON_OK");
    }

    [Fact]
    public async Task JavaScriptRunsThroughTheLanguageRunner()
    {
        await AssertScriptRuns("node", ".js", "console.log('KEYCE_NODE_OK')", "KEYCE_NODE_OK");
    }

    [Fact]
    public async Task TypeScriptRunsThroughNode()
    {
        await AssertScriptRuns("node", ".ts", "const value: string = 'KEYCE_TS_OK'; console.log(value)", "KEYCE_TS_OK");
    }

    [Fact]
    public async Task PhpScriptRunsThroughTheLanguageRunner()
    {
        await AssertScriptRuns("php", ".php", "<?php echo 'KEYCE_PHP_OK';", "KEYCE_PHP_OK");
    }

    [Fact]
    public async Task CppProgramCompilesAndRunsThroughTheLanguageRunner()
    {
        await AssertScriptRuns("g++", ".cpp", "#include <iostream>\nint main(){std::cout << \"KEYCE_CPP_OK\";}", "KEYCE_CPP_OK");
    }

    [Fact]
    public async Task CProgramCompilesAndRunsThroughTheLanguageRunner()
    {
        await AssertScriptRuns("gcc", ".c", "#include <stdio.h>\nint main(){puts(\"KEYCE_C_OK\");}", "KEYCE_C_OK");
    }

    private async Task AssertScriptRuns(string requiredTool, string extension, string source, string expectedOutput)
    {
        var tool = tools.Inspect(requiredTool);
        Assert.True(tool.IsAvailable, tool.Message);

        var directory = Path.Combine(Path.GetTempPath(), "KeyceCodeLanguageTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "Demo" + extension);
        try
        {
            await File.WriteAllTextAsync(filePath, source);
            var command = languageRunner.Resolve(filePath);
            Assert.NotNull(command);
            Assert.All(languageRunner.CheckTools(command!), status => Assert.True(status.IsAvailable, status.Message));

            var output = new StringBuilder();
            var runner = new ScriptRunnerService();
            runner.OutputReceived += (text, _) => output.Append(text);
            var exitCode = await runner.RunAsync(command!, directory);

            Assert.True(exitCode == 0, $"Commande : {command.Executable} {command.Arguments}\nCode de sortie {exitCode}. Sortie : {output}");
            Assert.Contains(expectedOutput, output.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }
}
