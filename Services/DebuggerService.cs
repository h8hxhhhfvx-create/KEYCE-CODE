using System.Diagnostics;
using System.Text;
using System.Text.Json;
using KeyceCode.Models;

namespace KeyceCode.Services;

public sealed class DebuggerService : IAsyncDisposable
{
    private Process? adapter;
    private StreamWriter? input;
    private int sequence;

    public event Action<string>? OutputReceived;
    public bool IsRunning => adapter is { HasExited: false };

    public string? FindAdapter()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("KEYCE_VSDBG_PATH"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio", "18", "Community", "Common7", "IDE", "vsdbg", "vsdbg.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio", "2022", "Community", "Common7", "IDE", "vsdbg", "vsdbg.exe")
        };
        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }

    public async Task StartAsync(string program, string workingDirectory, IReadOnlyList<DebugBreakpoint> breakpoints, CancellationToken cancellationToken = default)
    {
        if (IsRunning) throw new InvalidOperationException("Un débogage est déjà en cours.");
        var adapterPath = FindAdapter() ?? throw new FileNotFoundException("vsdbg.exe est introuvable. Installez les outils de débogage .NET.");
        if (!File.Exists(program)) throw new FileNotFoundException("Programme à déboguer introuvable.", program);
        adapter = new Process
        {
            StartInfo = new ProcessStartInfo(adapterPath, "--interpreter=vscode")
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };
        adapter.Exited += (_, _) => OutputReceived?.Invoke("Débogage terminé.\r\n");
        if (!adapter.Start()) throw new InvalidOperationException("Impossible de démarrer vsdbg.");
        input = adapter.StandardInput;
        _ = ReadMessagesAsync(adapter.StandardOutput, cancellationToken);
        _ = ReadErrorsAsync(adapter.StandardError, cancellationToken);

        await SendAsync("initialize", new { clientID = "keyce-code", adapterID = "coreclr", linesStartAt1 = true, columnsStartAt1 = true, pathFormat = "path" });
        await SendAsync("launch", new { program, cwd = workingDirectory, stopAtEntry = false, justMyCode = true, console = "internalConsole" });
        foreach (var group in breakpoints.GroupBy(item => Path.GetFullPath(item.FilePath), StringComparer.OrdinalIgnoreCase))
            await SendAsync("setBreakpoints", new { source = new { path = group.Key }, breakpoints = group.Select(item => new { line = item.Line }).ToArray() });
        await SendAsync("configurationDone", new { });
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;
        try { await SendAsync("disconnect", new { terminateDebuggee = true, restart = false }); }
        catch { }
        try { adapter?.Kill(entireProcessTree: true); } catch { }
    }

    private async Task SendAsync(string command, object arguments)
    {
        if (input is null) throw new InvalidOperationException("Le débogueur n’est pas démarré.");
        var payload = JsonSerializer.Serialize(new { seq = ++sequence, type = "request", command, arguments });
        var bytes = Encoding.UTF8.GetByteCount(payload);
        await input.WriteAsync($"Content-Length: {bytes}\r\n\r\n{payload}");
        await input.FlushAsync();
    }

    private async Task ReadMessagesAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && await reader.ReadLineAsync(cancellationToken) is { } header)
            {
                if (!header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) continue;
                if (!int.TryParse(header[15..].Trim(), out var length)) continue;
                await reader.ReadLineAsync(cancellationToken);
                var buffer = new char[length];
                var offset = 0;
                while (offset < length)
                {
                    var read = await reader.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
                    if (read == 0) break;
                    offset += read;
                }
                if (offset > 0) OutputReceived?.Invoke(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(buffer, 0, offset)) + "\r\n");
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    private async Task ReadErrorsAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        try { while (!cancellationToken.IsCancellationRequested && await reader.ReadLineAsync(cancellationToken) is { } line) OutputReceived?.Invoke(line + "\r\n"); }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        input?.Dispose();
        adapter?.Dispose();
    }
}
