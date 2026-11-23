using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class CodeAnalysisDiagnosticCoverageTests
{
    private readonly CodeAnalysisService service = new();

    [Fact]
    public void ReportsSyntaxErrorsWithSourcePosition()
    {
        var diagnostics = service.Analyze("class Demo { static void Main() { int value = 1 } }");

        var diagnostic = Assert.Single(diagnostics, item => item.Code == "CS1002");
        Assert.True(diagnostic.Line > 0);
        Assert.True(diagnostic.Column > 0);
    }

    [Fact]
    public void ReportsWarningsAlongsideValidSource()
    {
        var diagnostics = service.Analyze("class Demo { static void Main() { int unused = 1; } }");

        Assert.Contains(diagnostics, item => item.Severity == "Warning");
    }

    [Fact]
    public void IgnoresDiagnosticsWithoutSourceLocation()
    {
        var diagnostics = service.Analyze("class Demo { static void Main() { } }");

        Assert.All(diagnostics, item =>
        {
            Assert.True(item.Line > 0);
            Assert.True(item.Column > 0);
        });
    }

    [Fact]
    public void AnalyzesUnsavedOrClosedFileContentWithoutFileAccess()
    {
        var diagnostics = service.Analyze("class Demo { static void Main() { int value = 1 } }");

        Assert.Contains(diagnostics, item => item.Code == "CS1002");
    }
}
