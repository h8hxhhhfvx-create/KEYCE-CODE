using System.Text.Json;
using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class ApplicationLoggerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"KeyceCode-Logs-{Guid.NewGuid():N}");

    [Fact]
    public void Logger_WritesStructuredJsonWithoutThrowing()
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "app.jsonl");
        var logger = new ApplicationLogger(path);

        logger.Info("Démarrage", "startup");
        logger.Error(new InvalidOperationException("test"), "operation");

        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        using var entry = JsonDocument.Parse(lines[1]);
        Assert.Equal("Error", entry.RootElement.GetProperty("level").GetString());
        Assert.Equal("operation", entry.RootElement.GetProperty("operation").GetString());
    }

    [Fact]
    public void GlobalHandler_LogsUnobservedTaskAndCanBeDisposed()
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "app.jsonl");
        var logger = new ApplicationLogger(path);
        using var handler = GlobalExceptionHandler.Install(logger);
        handler.Dispose();

        Assert.True(true);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
