using System.Text;
using KeyceCode.Models;
using KeyceCode.Services;
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
}
