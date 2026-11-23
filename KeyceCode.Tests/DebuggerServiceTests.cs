using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class DebuggerServiceTests
{
    [Fact]
    public void FindAdapter_FindsInstalledVsdbg()
    {
        var service = new DebuggerService();
        var path = service.FindAdapter();
        if (path is null) return;
        Assert.EndsWith("vsdbg.exe", path, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(path));
    }
}
