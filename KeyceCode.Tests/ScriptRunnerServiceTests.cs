using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class ScriptRunnerServiceTests
{
    [Fact]
    public void NewRunner_IsNotRunning()
        => Assert.False(new ScriptRunnerService().IsRunning);
}
