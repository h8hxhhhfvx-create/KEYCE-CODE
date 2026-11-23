using System.Diagnostics;
using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class ExtensionProcessSupervisorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"KeyceCode-ExtensionHost-{Guid.NewGuid():N}");

    [Fact]
    public async Task Supervisor_RestartsCrashedHostOnlyUpToTheConfiguredLimit()
    {
        Directory.CreateDirectory(root);
        using var supervisor = new ExtensionProcessSupervisor(_ => new ProcessStartInfo("cmd.exe", "/c exit 7"));

        var result = await supervisor.RunWithRecoveryAsync(root, maximumRestarts: 2);

        Assert.True(result.Started);
        Assert.Equal(7, result.ExitCode);
        Assert.Equal(2, result.RestartCount);
        Assert.False(result.Recovered);
    }

    [Fact]
    public async Task Supervisor_ReportsSuccessfulHostAndCanBeCancelled()
    {
        Directory.CreateDirectory(root);
        using var supervisor = new ExtensionProcessSupervisor(_ => new ProcessStartInfo("cmd.exe", "/c exit 0"));

        var result = await supervisor.RunWithRecoveryAsync(root);

        Assert.True(result.Started);
        Assert.Equal(0, result.ExitCode);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Supervisor_RejectsMissingExtensionDirectory()
    {
        using var supervisor = new ExtensionProcessSupervisor(_ => new ProcessStartInfo("cmd.exe", "/c exit 0"));

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => supervisor.RunWithRecoveryAsync(Path.Combine(root, "missing")));
    }

    [Fact]
    public async Task Supervisor_CancelsSlowHost()
    {
        Directory.CreateDirectory(root);
        using var supervisor = new ExtensionProcessSupervisor(_ => new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 10 > nul"));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => supervisor.RunWithRecoveryAsync(root, cancellationToken: cancellation.Token));
        Assert.False(supervisor.IsRunning);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
