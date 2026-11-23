using System.Text;
using KeyceCode.Models;
using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class ScriptRunnerServiceTests
{
    [Fact]
    public void NewRunner_IsNotRunning()
        => Assert.False(new ScriptRunnerService().IsRunning);

    [Fact]
    public async Task RunAsync_CapturesOutputAndExitCode()
    {
        var runner = new ScriptRunnerService();
        var output = new StringBuilder();
        runner.OutputReceived += (text, _) => output.Append(text);

        var exitCode = await runner.RunAsync(
            new LanguageCommand("Batch", "cmd", "/c echo KEYCE_RUNNER_OK"),
            Environment.CurrentDirectory);

        Assert.Equal(0, exitCode);
        Assert.Contains("KEYCE_RUNNER_OK", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(runner.IsRunning);
    }
}
