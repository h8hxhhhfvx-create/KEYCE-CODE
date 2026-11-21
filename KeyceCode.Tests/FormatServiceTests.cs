using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class FormatServiceTests
{
    [Fact]
    public void FormatCSharp_NormalizesIndentationAndSpacing()
    {
        var formatted = new FormatService().FormatCSharp("class Demo{static void Main(){System.Console.WriteLine(\"OK\");}}")
            .Replace("\r\n", "\n");

        Assert.Contains("class Demo\n{", formatted, StringComparison.Ordinal);
        Assert.Contains("    static void Main()", formatted, StringComparison.Ordinal);
        Assert.Contains("        System.Console.WriteLine(\"OK\");", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatCSharp_UsesConfiguredIndentationSize()
    {
        var formatted = new FormatService().FormatCSharp("class Demo{void Run(){int value=1;}}", 2);
        Assert.Contains("  void Run()", formatted, StringComparison.Ordinal);
        Assert.Contains("    int value = 1;", formatted, StringComparison.Ordinal);
    }
}
