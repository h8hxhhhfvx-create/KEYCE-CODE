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
