using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class CodeRenameServiceTests
{
    [Fact]
    public void Rename_ChangesIdentifiersButNotStringsOrComments()
    {
        const string source = "class Demo { int value; void Run() { value = 1; } } // value\nstring text = \"value\";";
        var result = new CodeRenameService().Rename(source, source.IndexOf("value", StringComparison.Ordinal), "amount");
        Assert.NotNull(result);
        Assert.Equal(2, result!.Occurrences);
        Assert.Contains("amount = 1", result.Text, StringComparison.Ordinal);
        Assert.Contains("// value", result.Text, StringComparison.Ordinal);
        Assert.Contains("\"value\"", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Rename_RejectsInvalidIdentifier()
    {
        var service = new CodeRenameService();

        Assert.Throws<ArgumentException>(() => service.Rename("class Demo { }", 6, "not-valid"));
    }

    [Fact]
    public void Rename_ReturnsNullWhenCaretIsNotOnAnIdentifier()
    {
        var source = "class Demo { }";

        var result = new CodeRenameService().Rename(source, source.IndexOf('{'), "Other");

        Assert.Null(result);
    }
}
