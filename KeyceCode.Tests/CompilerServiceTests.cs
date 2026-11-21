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
}
