using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class LanguageRunnerServiceTests
{
    [Theory]
    [InlineData("demo.py", "Python", "python")]
    [InlineData("demo.js", "JavaScript", "node")]
    [InlineData("demo.go", "Go", "go")]
    [InlineData("demo.java", "Java", "java")]
    public void Resolve_ReturnsCommandForKnownLanguage(string fileName, string language, string executable)
    {
        var command = new LanguageRunnerService().Resolve(Path.Combine(Path.GetTempPath(), fileName));
        Assert.NotNull(command);
        Assert.Equal(language, command.Language);
        Assert.Equal(executable, command.Executable);
    }

    [Fact]
    public void Resolve_ReturnsNullForUnknownExtension()
        => Assert.Null(new LanguageRunnerService().Resolve("demo.unknown"));
}
