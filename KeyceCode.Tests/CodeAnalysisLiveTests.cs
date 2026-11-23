using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class CodeAnalysisLiveTests
{
    [Fact]
    public void Analyze_ReturnsUpdatedDiagnosticsAfterTextChanges()
    {
        var service = new CodeAnalysisService();
        Assert.Empty(service.Analyze("class Demo { static void Main() { } }"));
        Assert.Contains(service.Analyze("class Demo { static void Main() { int value = 1 } }"), item => item.Code == "CS1002");
    }
}
