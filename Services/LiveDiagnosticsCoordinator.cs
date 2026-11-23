using KeyceCode.Models;

namespace KeyceCode.Services;

/// <summary>
/// Coordonne les analyses en direct : une nouvelle saisie invalide les résultats
/// précédents et empêche une analyse lente de revenir après une version plus récente.
/// </summary>
public sealed class LiveDiagnosticsCoordinator : IDisposable
{
    private readonly object sync = new();
    private CancellationTokenSource? currentCancellation;
    private long generation;
    private bool disposed;

    public async Task<IReadOnlyList<Diagnostic>?> AnalyzeAsync(
        string source,
        Func<string, IReadOnlyList<Diagnostic>> analyzer,
        TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        CancellationTokenSource localCancellation;
        long localGeneration;
        lock (sync)
        {
            ThrowIfDisposed();
            currentCancellation?.Cancel();
            currentCancellation?.Dispose();
            currentCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            localCancellation = currentCancellation;
            localGeneration = ++generation;
        }

        try
        {
            await Task.Delay(delay, localCancellation.Token).ConfigureAwait(false);
            var diagnostics = await Task.Run(() => analyzer(source), localCancellation.Token).ConfigureAwait(false);
            lock (sync)
            {
                if (localCancellation.IsCancellationRequested || localGeneration != generation || disposed)
                    return null;
            }
            return diagnostics;
        }
        catch (OperationCanceledException) when (localCancellation.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            lock (sync)
            {
                if (ReferenceEquals(currentCancellation, localCancellation))
                {
                    currentCancellation.Dispose();
                    currentCancellation = null;
                }
            }
        }
    }

    public void Cancel()
    {
        lock (sync)
        {
            generation++;
            currentCancellation?.Cancel();
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            generation++;
            currentCancellation?.Cancel();
            currentCancellation?.Dispose();
            currentCancellation = null;
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(LiveDiagnosticsCoordinator));
    }
}
