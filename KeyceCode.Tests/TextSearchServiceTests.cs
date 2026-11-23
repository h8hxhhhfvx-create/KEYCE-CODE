using KeyceCode.Models;
using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class TextSearchServiceTests
{
    private readonly TextSearchService service = new();

    [Fact]
    public void Find_IsCaseInsensitiveByDefaultAndCanRespectCase()
    {
        Assert.Equal(2, service.Find("Keyce KEYCE", "keyce").Count);
        Assert.Single(service.Find("Keyce KEYCE", "Keyce", new TextSearchOptions(MatchCase: true)));
    }

    [Fact]
    public void Find_WholeWordDoesNotMatchPartOfIdentifier()
    {
        var matches = service.Find("cat catalog cat", "cat", new TextSearchOptions(WholeWord: true));
        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void ReplaceAll_SupportsRegexAndReportsCount()
    {
        var result = service.ReplaceAll("value1 value22", "value\\d+", "item", new TextSearchOptions(UseRegularExpression: true));
        Assert.Equal("item item", result.Text);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Find_InvalidRegexExplainsTheError()
    {
        var error = Assert.Throws<ArgumentException>(() => service.Find("abc", "[", new TextSearchOptions(UseRegularExpression: true)));
        Assert.Contains("invalide", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
