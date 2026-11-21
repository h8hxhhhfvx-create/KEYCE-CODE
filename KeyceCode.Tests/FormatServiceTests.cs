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
}
