using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class ExtensionServiceTests
{
    [Fact]
    public void Load_CreatesExtensionDirectoryAndReturnsEmptyList()
    {
        var directory = Path.Combine(Path.GetTempPath(), "keyce-ext-" + Guid.NewGuid());
        try
        {
            var service = new ExtensionService(directory);
            Assert.Empty(service.Load(null));
            Assert.True(Directory.Exists(directory));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
