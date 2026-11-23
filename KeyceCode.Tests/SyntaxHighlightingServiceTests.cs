using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class SyntaxHighlightingServiceTests
{
    [Theory]
    [InlineData(".cs")]
    [InlineData(".js")]
    [InlineData(".html")]
    [InlineData(".css")]
    [InlineData(".json")]
    [InlineData(".xml")]
    [InlineData(".py")]
    [InlineData(".php")]
    [InlineData(".java")]
    [InlineData(".md")]
    [InlineData(".ps1")]
    public void Registry_ResolvesCommonExtensions(string extension)
    {
        Assert.True(new SyntaxHighlightingService().IsSupported(extension));
        Assert.NotNull(new SyntaxHighlightingService().GetDefinitionName(extension));
    }

    [Fact]
    public void IsSupported_NormalizesExtensionWithoutLeadingDot()
    {
        Assert.True(new SyntaxHighlightingService().IsSupported("cs"));
    }

    [Fact]
    public void Registry_RejectsUnknownExtension()
    {
        var service = new SyntaxHighlightingService();
        Assert.False(service.IsSupported(".keyce-unknown"));
        Assert.Null(service.GetDefinitionName(".keyce-unknown"));
    }
}
