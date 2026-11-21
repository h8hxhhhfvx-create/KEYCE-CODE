using System.ComponentModel;
using KeyceCode.Models;

namespace KeyceCode.Services;

public sealed class ScriptRunnerService
{
    private ProcessSession? session;
    private volatile bool busy;

    public event Action<string, OutputKind>? OutputReceived;
    public bool IsRunning => busy;

    public async Task<int> RunAsync(LanguageCommand command, string workingDirectory, CancellationToken cancellationToken = default)
    {
        if (busy) throw new InvalidOperationException("Un programme est déjà en cours.");
        busy = true;
        using var current = new ProcessSession(command.Executable, command.Arguments, workingDirectory);
        current.OutputReceived += (text, kind) => OutputReceived?.Invoke(text, kind);
        session = current;
        try { return await current.RunAsync(cancellationToken); }
        catch (Win32Exception) { throw new InvalidOperationException($"Impossible de lancer « {command.Executable} ». Vérifiez que l’outil est installé et présent dans le PATH."); }
        finally { session = null; busy = false; }
    }

    public void SendInput(string text) => session?.Write(text + Environment.NewLine);
    public void Stop() => session?.Kill();
}
