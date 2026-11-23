using KeyceCode.Models;
using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class LiveDiagnosticsCoordinatorTests
{
    [Fact]
    public async Task ANewAnalysisInvalidatesAnOlderSlowResult()
    {
        using var coordinator = new LiveDiagnosticsCoordinator();
        using var started = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var first = coordinator.AnalyzeAsync(
            "old",
            asyncSource =>
            {
                started.Set();
                release.Wait(TimeSpan.FromSeconds(10));
                return [new Diagnostic { Code = asyncSource }];
            },
            TimeSpan.Zero);

        Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
        var second = coordinator.AnalyzeAsync(
            "new",
            source => [new Diagnostic { Code = source }],
            TimeSpan.Zero);
        release.Set();

        var results = await Task.WhenAll(first, second);
        Assert.Null(results[0]);
        Assert.Equal("new", Assert.Single(results[1]!).Code);
    }

    [Fact]
    public async Task CancelPreventsApplyingDiagnostics()
    {
        using var coordinator = new LiveDiagnosticsCoordinator();
        var pending = coordinator.AnalyzeAsync(
            "source",
            source => [new Diagnostic { Code = source }],
            TimeSpan.FromMilliseconds(100));

        coordinator.Cancel();

        Assert.Null(await pending);
    }
}
