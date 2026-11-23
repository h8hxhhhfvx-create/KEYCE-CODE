using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class AutoCompleteServiceTests
{
    [Fact]
    public void GetSuggestions_ReturnsKeywordsAndIdentifiersMatchingPrefix()
    {
        var service = new AutoCompleteService();
        var suggestions = service.GetSuggestions("string customerName; con", 26);

        Assert.Contains("continue", suggestions);
        Assert.Contains("Console", service.GetSuggestions("Con", 3));
    }

    [Fact]
    public void GetCompletionItems_ProvidesSnippetWithSeparateInsertText()
    {
        var item = Assert.Single(new AutoCompleteService().GetCompletionItems("cw", 2), item => item.Kind == "Snippet");

        Assert.Equal("cw — Console.WriteLine", item.DisplayText);
        Assert.Equal("Console.WriteLine();", item.InsertText);
    }
}
