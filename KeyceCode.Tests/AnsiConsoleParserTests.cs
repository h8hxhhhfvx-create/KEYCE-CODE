using KeyceCode.Models;
using Xunit;

namespace KeyceCode.Tests;

public sealed class AnsiConsoleParserTests
{
    [Fact]
    public void Parse_MapsAnsiColorsAndRemovesEscapeCodes()
    {
        var segments = AnsiConsoleParser.Parse("normal \u001b[31merror\u001b[0m\u001b[32mok\u001b[0m", OutputKind.StdOut);

        Assert.Equal(3, segments.Count);
        Assert.Equal(("normal ", OutputKind.StdOut), (segments[0].Text, segments[0].Kind));
        Assert.Equal(("error", OutputKind.Error), (segments[1].Text, segments[1].Kind));
        Assert.Equal(("ok", OutputKind.Success), (segments[2].Text, segments[2].Kind));
    }

    [Fact]
    public void ConsoleBuffer_PreservesAnsiColorPerLineSegment()
    {
        var buffer = new ConsoleBuffer();
        buffer.Append("\u001b[33mWARNING\u001b[0m\n", OutputKind.StdOut);

        Assert.Single(buffer.Lines);
        Assert.Equal("WARNING", buffer.Lines[0].Text);
        Assert.Equal(OutputKind.Info, buffer.Lines[0].Kind);
    }

    [Fact]
    public void Parse_IgnoresCursorAndClearControlSequences()
    {
        var segments = AnsiConsoleParser.Parse("A\u001b[2K\u001b[1G B", OutputKind.StdOut);

        Assert.Single(segments);
        Assert.Equal("A B", segments[0].Text);
    }
}
