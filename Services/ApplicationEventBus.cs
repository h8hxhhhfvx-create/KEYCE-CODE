namespace KeyceCode.Services;

public sealed record ApplicationEvent(string Name, object? Data = null, DateTimeOffset? OccurredAt = null)
{
    public DateTimeOffset Timestamp { get; } = OccurredAt ?? DateTimeOffset.UtcNow;
}

public interface IApplicationEventBus
{
    IDisposable Subscribe(string name, Action<ApplicationEvent> handler);
    void Publish(string name, object? data = null);
}

/// <summary>Bus d’événements thread-safe, indépendant de WinForms/WPF.</summary>
public sealed class ApplicationEventBus : IApplicationEventBus
{
    private readonly object sync = new();
    private readonly Dictionary<string, List<Action<ApplicationEvent>>> handlers = new(StringComparer.Ordinal);

    public IDisposable Subscribe(string name, Action<ApplicationEvent> handler)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Le nom de l’événement est obligatoire.", nameof(name));
        ArgumentNullException.ThrowIfNull(handler);
        lock (sync)
        {
            if (!handlers.TryGetValue(name, out var list)) handlers[name] = list = [];
            list.Add(handler);
        }
        return new Subscription(this, name, handler);
    }

    public void Publish(string name, object? data = null)
    {
        Action<ApplicationEvent>[] snapshot;
        lock (sync)
            snapshot = handlers.TryGetValue(name, out var list) ? list.ToArray() : [];

        var eventData = new ApplicationEvent(name, data);
        foreach (var handler in snapshot) handler(eventData);
    }

    private void Unsubscribe(string name, Action<ApplicationEvent> handler)
    {
        lock (sync)
        {
            if (!handlers.TryGetValue(name, out var list)) return;
            list.Remove(handler);
            if (list.Count == 0) handlers.Remove(name);
        }
    }

    private sealed class Subscription(ApplicationEventBus owner, string name, Action<ApplicationEvent> handler) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0) owner.Unsubscribe(name, handler);
        }
    }
}
