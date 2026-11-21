using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class CodeSymbolServiceTests
{
    [Fact]
    public void GetSymbols_ReturnsDeclarationsInSourceOrder()
    {
        var symbols = new CodeSymbolService().GetSymbols("class Demo { public int Value { get; set; } static void Main() { } }");
        Assert.Equal(["Demo", "Value", "Main"], symbols.Select(symbol => symbol.Name));
        Assert.Equal("Classe", symbols[0].Kind);
        Assert.Equal("Méthode", symbols[2].Kind);
    }
}
