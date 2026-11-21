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
}
