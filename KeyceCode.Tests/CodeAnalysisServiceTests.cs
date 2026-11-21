using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class CodeAnalysisServiceTests
{
    private readonly CodeAnalysisService service = new();

    [Fact]
    public void ValidCSharpCode_HasNoErrors()
    {
        var diagnostics = service.Analyze("using System; class Demo { static void Main() { Console.WriteLine(\"OK\"); } }");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == "Error");
    }

    [Fact]
    public void InvalidCSharpCode_ReturnsLineAndColumn()
    {
        var diagnostics = service.Analyze("class Demo\n{\n    static void Main()\n    {\n        int value = 1\n    }\n}");

        var diagnostic = Assert.Single(diagnostics, item => item.Code == "CS1002");
        Assert.Equal(5, diagnostic.Line);
        Assert.True(diagnostic.Column > 0);
    }

    [Fact]
    public void Analysis_ExposesDiagnosticCodeAndMessage()
    {
        var diagnostics = service.Analyze("class Demo { static void Main() { Missing(); } }");

        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Code == "CS0103" && diagnostic.Message.Contains("Missing", StringComparison.Ordinal));
    }
}
