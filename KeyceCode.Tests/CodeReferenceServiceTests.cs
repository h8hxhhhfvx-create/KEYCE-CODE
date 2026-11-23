using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class CodeReferenceServiceTests
{
    [Fact]
    public void FindReferences_ReturnsIdentifiersButNotStringsOrComments()
    {
        const string source = "class Demo { int value; void Run() { value = 1; } } // value\nstring text = \"value\";";
        var offset = source.IndexOf("value", StringComparison.Ordinal);

        var references = new CodeReferenceService().FindReferences(source, offset);

        Assert.Equal(2, references.Count);
        Assert.All(references, reference => Assert.Equal("value", reference.Name));
    }

    [Fact]
    public void FindReferences_ReturnsEmptyWhenCaretIsNotOnIdentifier()
    {
        Assert.Empty(new CodeReferenceService().FindReferences("class Demo { }", 0));
    }
}
