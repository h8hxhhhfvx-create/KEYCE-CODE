using System.Text;
using KeyceCode.Models;
using KeyceCode.Services;
using KeyceCode.ViewModels;
using Xunit;

namespace KeyceCode.Tests;

public sealed class TerminalServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ForwardsStandardOutputAndExitCode()
    {
        var service = new TerminalService(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var output = new StringBuilder();
        service.OutputReceived += (text, _) => output.Append(text);

        var exitCode = await service.ExecuteAsync("echo KEYCE_OUTPUT");

        Assert.Equal(0, exitCode);
        Assert.Contains("KEYCE_OUTPUT", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesShellEnvironmentBetweenCommands()
    {
        using var service = new TerminalService(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var output = new StringBuilder();
        service.OutputReceived += (text, _) => output.Append(text);

        Assert.Equal(0, await service.ExecuteAsync("set KEYCE_PERSISTENCE=ACTIVE"));
        Assert.Equal(0, await service.ExecuteAsync("echo %KEYCE_PERSISTENCE%"));

        Assert.Contains("ACTIVE", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithPowerShellProfile_ForwardsOutputAndExitCode()
    {
        using var service = new TerminalService(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        service.SetShellProfile("PowerShell");
        var output = new StringBuilder();
        service.OutputReceived += (text, _) => output.Append(text);

        var exitCode = await service.ExecuteAsync("Write-Output KEYCE_POWERSHELL");

        Assert.Equal(0, exitCode);
        Assert.Contains("KEYCE_POWERSHELL", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetShellProfile_NormalizesSupportedProfiles()
    {
        using var service = new TerminalService(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        service.SetShellProfile("PowerShell");
        Assert.Equal("PowerShell", service.ShellProfile);
        service.SetShellProfile("unknown");
        Assert.Equal("cmd", service.ShellProfile);
    }

    [Fact]
    public async Task SetWorkingDirectory_PersistsInCmdSession()
    {
        var root = Path.Combine(Path.GetTempPath(), "KeyceTerminalTests", Guid.NewGuid().ToString("N"));
        var child = Path.Combine(root, "child");
        Directory.CreateDirectory(child);
        try
        {
            using var service = new TerminalService(root);
            var output = new StringBuilder();
            service.OutputReceived += (text, _) => output.Append(text);

            Assert.Equal(0, await service.ExecuteAsync("echo READY"));
            service.SetWorkingDirectory(child);
            Assert.Equal(0, await service.ExecuteAsync("echo %CD%"));

            Assert.Contains(child, output.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task SetWorkingDirectory_UsesPowerShellLocationSyntax()
    {
        var root = Path.Combine(Path.GetTempPath(), "KeyceTerminalTests", Guid.NewGuid().ToString("N"));
        var child = Path.Combine(root, "child");
        Directory.CreateDirectory(child);
        try
        {
            using var service = new TerminalService(root);
            service.SetShellProfile("PowerShell");
            var output = new StringBuilder();
            service.OutputReceived += (text, _) => output.Append(text);

            Assert.Equal(0, await service.ExecuteAsync("Write-Output READY"));
            service.SetWorkingDirectory(child);
            Assert.Equal(0, await service.ExecuteAsync("(Get-Location).Path"));

            Assert.Contains(child, output.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsInteractiveInputToCmd()
    {
        using var service = new TerminalService(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var output = new StringBuilder();
        service.OutputReceived += (text, _) => output.Append(text);

        var running = service.ExecuteAsync("python -c \"value=input(); print('INPUT:'+value)\"");
        await WaitUntilRunning(service);
        await Task.Delay(150);
        service.SendInput("KEYCE_INPUT");
        var exitCode = await running;

        Assert.Equal(0, exitCode);
        Assert.Contains("KEYCE_INPUT", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsInteractiveInputToPowerShell()
    {
        using var service = new TerminalService(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        service.SetShellProfile("PowerShell");
        var output = new StringBuilder();
        service.OutputReceived += (text, _) => output.Append(text);

        var running = service.ExecuteAsync("$value = Read-Host 'Value'; Write-Output $value");
        await WaitUntilRunning(service);
        await Task.Delay(150);
        service.SendInput("KEYCE_PS_INPUT");
        var exitCode = await running;

        Assert.Equal(0, exitCode);
        Assert.Contains("KEYCE_PS_INPUT", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stop_EndsALongRunningTerminalCommand()
    {
        using var service = new TerminalService(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var running = service.ExecuteAsync("ping 127.0.0.1 -n 20 >nul");
        await WaitUntilRunning(service);

        service.Stop();
        var exitCode = await running;

        Assert.NotEqual(0, exitCode);
        Assert.False(service.IsRunning);
    }

    private static async Task WaitUntilRunning(TerminalService service)
    {
        for (var attempt = 0; attempt < 30 && !service.IsRunning; attempt++)
            await Task.Delay(25);
        Assert.True(service.IsRunning);
    }

    [Fact]
    public async Task TerminalViewModel_DisplaysCommandOutput()
    {
        var viewModel = new TerminalViewModel(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        viewModel.InputText = "echo KEYCE_VIEWMODEL";

        viewModel.SubmitCommand.Execute(null);
        for (var attempt = 0; attempt < 20 && !viewModel.Log.Lines.Any(line => line.Text.Contains("KEYCE_VIEWMODEL", StringComparison.OrdinalIgnoreCase)); attempt++)
            await Task.Delay(100);

        Assert.Contains(viewModel.Log.Lines, line => line.Text.Contains("KEYCE_VIEWMODEL", StringComparison.OrdinalIgnoreCase));
    }
}
