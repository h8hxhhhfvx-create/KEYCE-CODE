using KeyceCode.Models;
using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class ToolDetectionServiceTests
{
    [Fact]
    public void DetectsAnExecutableAvailableInPath()
    {
        var result = new ToolDetectionService().Inspect("dotnet");

        Assert.True(result.IsAvailable, result.Message);
        Assert.False(string.IsNullOrWhiteSpace(result.ResolvedPath));
    }

    [Fact]
    public void ExplainsMissingExecutable()
    {
        var result = new ToolDetectionService().Inspect("keyce-tool-that-does-not-exist");

        Assert.False(result.IsAvailable);
        Assert.Contains("PATH", result.Message);
    }

    [Fact]
    public void NativeLanguageRequiresItsCompilerInsteadOfOnlyCmd()
    {
        var command = new LanguageRunnerService().Resolve(Path.Combine(Path.GetTempPath(), "demo.cpp"));

        Assert.NotNull(command);
        Assert.DoesNotContain("cmd", command.RequiredTools!, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(command.RequiredTools!, tool =>
            tool.Equals("g++", StringComparison.OrdinalIgnoreCase)
            || tool.EndsWith("vcvars64.bat", StringComparison.OrdinalIgnoreCase));
    }
}
