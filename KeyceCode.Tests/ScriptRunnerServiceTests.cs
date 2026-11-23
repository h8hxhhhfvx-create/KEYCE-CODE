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

    [Fact]
    public async Task RunAsync_ReturnsNonZeroCodeForFailedProgram()
    {
        var runner = new ScriptRunnerService();

        var exitCode = await runner.RunAsync(
            new LanguageCommand("Batch", "cmd", "/c exit /b 7"),
            Environment.CurrentDirectory);

        Assert.Equal(7, exitCode);
        Assert.False(runner.IsRunning);
    }

    [Fact]
    public async Task RunAsync_ForwardsInteractiveInput()
    {
        var runner = new ScriptRunnerService();
        var output = new StringBuilder();
        runner.OutputReceived += (text, _) => output.Append(text);
        var running = runner.RunAsync(
            new LanguageCommand("Batch", "cmd", "/v:on /c \"set /p value= & echo INPUT:!value!\""),
            Environment.CurrentDirectory);

        await Task.Delay(100);
        runner.SendInput("HELLO");
        var exitCode = await running;

        Assert.Equal(0, exitCode);
        Assert.Contains("INPUT:HELLO", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stop_KillsALongRunningProgram()
    {
        var runner = new ScriptRunnerService();
        var running = runner.RunAsync(
            new LanguageCommand("Batch", "cmd", "/c ping 127.0.0.1 -n 20 >nul"),
            Environment.CurrentDirectory);

        await Task.Delay(120);
        runner.Stop();
        var exitCode = await running;

        Assert.NotEqual(0, exitCode);
        Assert.False(runner.IsRunning);
    }
}
