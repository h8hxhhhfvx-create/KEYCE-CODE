using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class AiAgentServiceTests
{
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
}
