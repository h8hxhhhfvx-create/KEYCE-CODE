using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class LanguageRunnerServiceTests
{
    [Theory]
    [InlineData("demo.py", "Python", "python")]
    [InlineData("demo.js", "JavaScript", "node")]
    [InlineData("demo.go", "Go", "go")]
    [InlineData("demo.java", "Java", "cmd")]
    [InlineData("demo.cpp", "C++", "cmd")]
    [InlineData("demo.rs", "Rust", "cmd")]
    [InlineData("demo.ts", "TypeScript", "node")]
    public void Resolve_ReturnsCommandForKnownLanguage(string fileName, string language, string executable)
    {
        var command = new LanguageRunnerService().Resolve(Path.Combine(Path.GetTempPath(), fileName));
        Assert.NotNull(command);
        Assert.Equal(language, command.Language);
        Assert.Equal(executable, command.Executable);
    }

    [Fact]
    public void Resolve_JavaCompilesThenRunsTheClass()
    {
        var path = Path.Combine(Path.GetTempPath(), "Demo.java");
        var command = new LanguageRunnerService().Resolve(path);

        Assert.NotNull(command);
        Assert.Equal("cmd", command.Executable);
        Assert.Contains("javac", command.Arguments, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("java -cp", command.Arguments, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { "javac", "java" }, command.RequiredTools);
    }

    [Fact]
    public void Resolve_NativeCompilationsUseDifferentTemporaryOutputs()
    {
        var service = new LanguageRunnerService();
        var first = service.Resolve(Path.Combine(Path.GetTempPath(), "Demo.c"));
        var second = service.Resolve(Path.Combine(Path.GetTempPath(), "Demo.c"));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first!.Arguments, second!.Arguments);
    }

    [Fact]
    public void Resolve_ReturnsNullForUnknownExtension()
        => Assert.Null(new LanguageRunnerService().Resolve("demo.unknown"));
}
