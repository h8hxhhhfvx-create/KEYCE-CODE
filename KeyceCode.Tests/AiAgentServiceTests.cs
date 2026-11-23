using KeyceCode.Models;
using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class AiAgentServiceTests
{
    [Fact]
    public async Task GeneratePlan_UsesConfiguredAiRouter()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "keyce-agent-" + Guid.NewGuid())).FullName;
        var router = new AiRouterService([(
            new AiProviderConfiguration("Test", "https://example.invalid", "test-model", "not-used"),
            new PlanProvider())]);
        try
        {
            var service = new AiAgentService(router: router);
            var plan = await service.GeneratePlanAsync("http://unused", "unused", root, "Créer Demo.cs");
            var change = Assert.Single(plan);
            Assert.Equal("Demo.cs", change.RelativePath);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ParsePlan_RejectsPathOutsideProject()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "keyce-agent-" + Guid.NewGuid())).FullName;
        try { Assert.Throws<InvalidOperationException>(() => new AiAgentService().ParsePlan(root, "<file path=\"..\\secret.txt\">x</file>")); }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ParsePlan_ReadsRelativeFileBlock()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "keyce-agent-" + Guid.NewGuid())).FullName;
        try
        {
            var plan = new AiAgentService().ParsePlan(root, "<file path=\"src\\Demo.cs\">class Demo { }</file>");
            var change = Assert.Single(plan);
            Assert.Equal(Path.Combine("src", "Demo.cs"), change.RelativePath);
            Assert.Null(change.ExistingContent);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ReadProjectContext_IncludesRelevantFilesAndSkipsBuildFolders()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "keyce-agent-" + Guid.NewGuid())).FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "Demo.cs"), "class Demo { }");
            Directory.CreateDirectory(Path.Combine(root, "bin"));
            await File.WriteAllTextAsync(Path.Combine(root, "bin", "Hidden.cs"), "secret");
            var context = new AiAgentService().ReadProjectContext(root);
            Assert.Contains("Demo.cs", context, StringComparison.Ordinal);
            Assert.DoesNotContain("Hidden.cs", context, StringComparison.Ordinal);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ParsePlan_RejectsDuplicateFileBlocks()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "keyce-agent-" + Guid.NewGuid())).FullName;
        try
        {
            var response = "<file path=\"Demo.cs\">one</file><file path=\"./Demo.cs\">two</file>";

            Assert.Throws<InvalidOperationException>(() => new AiAgentService().ParsePlan(root, response));
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class PlanProvider : IAiChatProvider
    {
        public string Name => "Test";
        public Task<string> ChatAsync(string model, IReadOnlyList<(string Role, string Content)> messages, CancellationToken cancellationToken = default)
            => Task.FromResult("<file path=\"Demo.cs\">class Demo { }</file>");
    }
}
