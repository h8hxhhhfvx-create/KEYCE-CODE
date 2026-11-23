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
            new AgentChange("Existing.cs", "new", "contenu obsolète fourni par un plan périmé"),
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

    [Fact]
    public async Task ApplyPlan_RollsBackEarlierFilesWhenALaterWriteFails()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "keyce-agent-" + Guid.NewGuid())).FullName;
        Directory.CreateDirectory(Path.Combine(root, "Blocked"));
        var service = new AiAgentService();
        var plan = new[]
        {
            new AgentChange("First.cs", "created", null),
            new AgentChange("Blocked", "cannot replace a directory", null)
        };
        try
        {
            var exception = await Assert.ThrowsAnyAsync<Exception>(() => service.ApplyPlanAsync(root, plan));
            Assert.True(exception is IOException or UnauthorizedAccessException, exception.GetType().FullName);
            Assert.False(File.Exists(Path.Combine(root, "First.cs")));
            Assert.True(Directory.Exists(Path.Combine(root, "Blocked")));
        }
        finally { Directory.Delete(root, true); }
    }
}
