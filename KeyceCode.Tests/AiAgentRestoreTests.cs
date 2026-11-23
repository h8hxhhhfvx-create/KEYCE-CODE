using KeyceCode.Models;
using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class AiAgentRestoreTests
{
    [Fact]
    public async Task RestoreLastPlan_RestoresExistingAndRemovesNewFiles()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "keyce-agent-" + Guid.NewGuid())).FullName;
        var existing = Path.Combine(root, "Existing.cs");
        await File.WriteAllTextAsync(existing, "old");
        var service = new AiAgentService();
        var plan = new[]
        {
            new AgentChange("Existing.cs", "new", "old"),
            new AgentChange("New.cs", "created", null)
        };
        try
        {
            await service.ApplyPlanAsync(root, plan);
            await service.RestoreLastPlanAsync(root);
            Assert.Equal("old", await File.ReadAllTextAsync(existing));
            Assert.False(File.Exists(Path.Combine(root, "New.cs")));
        }
        finally { Directory.Delete(root, true); }
    }
}
