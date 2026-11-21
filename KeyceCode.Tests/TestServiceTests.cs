using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class TestServiceTests
{
    [Fact]
    public void ResolveTestProject_FindsProjectInDirectory()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var project = new TestService().ResolveTestProject(Path.Combine(root, "KeyceCode.Tests"));
        Assert.EndsWith("KeyceCode.Tests.csproj", project, StringComparison.OrdinalIgnoreCase);
    }
}
