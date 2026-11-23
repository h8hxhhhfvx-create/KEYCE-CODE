using System.Diagnostics;

namespace KeyceCode.Services;

public sealed record ExtensionProcessResult(
    bool Started,
    int ExitCode,
    int RestartCount,
    bool Recovered,
    string? Error);

/// <summary>Surveille un hôte d’extension isolé et le relance selon une limite explicite.</summary>
public sealed class ExtensionProcessSupervisor : IDisposable
{
    private readonly Func<string, ProcessStartInfo> startInfoFactory;
    private readonly object sync = new();
    private Process? process;
    private bool disposed;

    public ExtensionProcessSupervisor(Func<string, ProcessStartInfo>? startInfoFactory = null)
    {
        this.startInfoFactory = startInfoFactory ?? CreateDefaultStartInfo;
    }

    public bool IsRunning
    {
        get
        {
            lock (sync) return process is { HasExited: false };
        }
    }

    public async Task<ExtensionProcessResult> RunWithRecoveryAsync(
        string extensionDirectory,
        int maximumRestarts = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(extensionDirectory)) throw new ArgumentException("Le dossier de l’extension est obligatoire.", nameof(extensionDirectory));
        if (maximumRestarts < 0) throw new ArgumentOutOfRangeException(nameof(maximumRestarts));
        if (!Directory.Exists(extensionDirectory)) throw new DirectoryNotFoundException(extensionDirectory);

        var restartCount = 0;
        while (true)
        {
            var result = await RunOnceAsync(extensionDirectory, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode == 0 || restartCount >= maximumRestarts)
                return result with { RestartCount = restartCount, Recovered = restartCount > 0 && result.ExitCode == 0 };
            restartCount++;
        }
    }

    public void Stop()
    {
        lock (sync)
        {
            if (process is not { HasExited: false }) return;
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            Stop();
            process?.Dispose();
            process = null;
        }
    }

    private async Task<ExtensionProcessResult> RunOnceAsync(string extensionDirectory, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ExtensionProcessSupervisor));
        }

        using var started = new Process { StartInfo = startInfoFactory(extensionDirectory), EnableRaisingEvents = true };
        started.StartInfo.UseShellExecute = false;
        lock (sync) process = started;
        try
        {
            if (!started.Start()) return new ExtensionProcessResult(false, -1, 0, false, "Impossible de démarrer l’hôte d’extension.");
            await started.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return new ExtensionProcessResult(true, started.ExitCode, 0, false, started.ExitCode == 0 ? null : "L’hôte d’extension s’est arrêté avec une erreur.");
        }
        catch (OperationCanceledException)
        {
            try { if (!started.HasExited) started.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new ExtensionProcessResult(false, -1, 0, false, exception.Message);
        }
        finally
        {
            lock (sync)
            {
                if (ReferenceEquals(process, started)) process = null;
            }
        }
    }

    private static ProcessStartInfo CreateDefaultStartInfo(string extensionDirectory)
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("Le chemin de l’exécutable KEYCE CODE est introuvable.");
        return new ProcessStartInfo(executable, $"--keyce-extension-host \"{extensionDirectory}\"");
    }
}

public static class ExtensionHostRunner
{
    public static int Run(string extensionDirectory)
    {
        if (!Directory.Exists(extensionDirectory)) return 2;
        var parent = Directory.GetParent(extensionDirectory)?.FullName;
        if (parent is null) return 2;
        var result = new ExtensionService(parent).Load(null)
            .FirstOrDefault(item => string.Equals(Path.GetDirectoryName(item.AssemblyPath), Path.GetFullPath(extensionDirectory), StringComparison.OrdinalIgnoreCase));
        return result is { Loaded: true } ? 0 : 1;
    }
}
