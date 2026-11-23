using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class ApplicationEventAndCancellationTests
{
    [Fact]
    public void EventBus_PublishesSnapshotAndStopsAfterUnsubscribe()
    {
        var bus = new ApplicationEventBus();
        var received = new List<ApplicationEvent>();
        using var subscription = bus.Subscribe("document.changed", received.Add);

        bus.Publish("document.changed", "Program.cs");

        Assert.Single(received);
        Assert.Equal("Program.cs", received[0].Data);
        Assert.Equal("document.changed", received[0].Name);
        subscription.Dispose();
        bus.Publish("document.changed");
        Assert.Single(received);
    }

    [Fact]
    public void EventBus_RejectsInvalidSubscriptions()
    {
        var bus = new ApplicationEventBus();

        Assert.Throws<ArgumentException>(() => bus.Subscribe("", _ => { }));
        Assert.Throws<ArgumentNullException>(() => bus.Subscribe("event", null!));
    }

    [Fact]
    public void Registry_BeginCancelsPreviousOperationWithSameName()
    {
        using var registry = new OperationCancellationRegistry();
        var first = registry.Begin("analysis");
        var second = registry.Begin("analysis");

        Assert.True(first.IsCancellationRequested);
        Assert.False(second.IsCancellationRequested);
        Assert.True(registry.Cancel("analysis"));
        Assert.True(second.IsCancellationRequested);
        registry.Complete("analysis");
        Assert.False(registry.Cancel("analysis"));
    }

    [Fact]
    public void Registry_PropagatesExternalCancellationAndDispose()
    {
        using var external = new CancellationTokenSource();
        var registry = new OperationCancellationRegistry();
        var token = registry.Begin("build", external.Token);
        external.Cancel();
        Assert.True(token.IsCancellationRequested);

        registry.Dispose();
        Assert.Throws<ObjectDisposedException>(() => registry.Begin("build"));
    }
}
