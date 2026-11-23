namespace KeyceCode.Services;

/// <summary>Gère les opérations nommées : démarrer une nouvelle opération annule l’ancienne.</summary>
public sealed class OperationCancellationRegistry : IDisposable
{
    private readonly object sync = new();
    private readonly Dictionary<string, CancellationTokenSource> operations = new(StringComparer.OrdinalIgnoreCase);
    private bool disposed;

    public CancellationToken Begin(string operationName, CancellationToken externalToken = default)
    {
        if (string.IsNullOrWhiteSpace(operationName)) throw new ArgumentException("Le nom de l’opération est obligatoire.", nameof(operationName));
        lock (sync)
        {
            ThrowIfDisposed();
            if (operations.Remove(operationName, out var previous))
            {
                previous.Cancel();
                previous.Dispose();
            }
            var source = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            operations[operationName] = source;
            return source.Token;
        }
    }

    public bool Cancel(string operationName)
    {
        lock (sync)
        {
            if (!operations.TryGetValue(operationName, out var source)) return false;
            source.Cancel();
            return true;
        }
    }

    public void Complete(string operationName)
    {
        lock (sync)
        {
            if (!operations.Remove(operationName, out var source)) return;
            source.Dispose();
        }
    }

    public void CancelAll()
    {
        lock (sync)
            foreach (var source in operations.Values) source.Cancel();
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            foreach (var source in operations.Values)
            {
                source.Cancel();
                source.Dispose();
            }
            operations.Clear();
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(OperationCancellationRegistry));
    }
}
