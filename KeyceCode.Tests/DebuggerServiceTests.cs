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

    [Fact]
    public void FindAdapter_UsesPreferredConfiguredPath()
    {
        var directory = Directory.CreateTempSubdirectory("KeyceCodeVsdbg");
        try
        {
            var path = Path.Combine(directory.FullName, "vsdbg.exe");
            File.WriteAllText(path, "configured-path");
            var service = new DebuggerService { PreferredAdapterPath = path };

            Assert.Equal(Path.GetFullPath(path), service.FindAdapter());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
