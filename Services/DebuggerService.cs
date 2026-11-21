using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KeyceCode.Models;

namespace KeyceCode.Services;

public sealed class DebuggerService : IAsyncDisposable
{
    private Process? adapter;
    private StreamWriter? input;
    private int sequence;
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private TaskCompletionSource<bool>? handshakeCompleted;
    private TaskCompletionSource<bool>? initializeCompleted;
    private TaskCompletionSource<bool>? initializedEvent;
    private TaskCompletionSource<bool>? configurationCompleted;
    private int stoppedThreadId;
    private readonly List<DebugFrame> currentFrames = new();
    private IReadOnlyList<DebugBreakpoint> activeBreakpoints = [];
    private string? debugProgramPath;

    public event Action<string>? OutputReceived;
    public event Action<DebugSnapshot>? SnapshotUpdated;
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
        activeBreakpoints = breakpoints.ToArray();
        debugProgramPath = Path.GetFullPath(program);
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
        handshakeCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        initializeCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        initializedEvent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        configurationCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = ReadMessagesAsync(adapter.StandardOutput.BaseStream, cancellationToken);
        _ = ReadErrorsAsync(adapter.StandardError, cancellationToken);

        await SendAsync("initialize", new { clientID = "keyce-code", clientName = "KEYCE CODE", adapterID = "coreclr", linesStartAt1 = true, columnsStartAt1 = true, pathFormat = "path" });
        await initializeCompleted.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        await SendAsync("launch", new
        {
            program,
            cwd = workingDirectory,
            stopAtEntry = false,
            justMyCode = false,
            requireExactSource = false,
            console = "internalConsole",
            symbolOptions = new
            {
                searchPaths = new[] { Path.GetDirectoryName(program) ?? workingDirectory },
                searchMicrosoftSymbolServer = false
            }
        });
        await handshakeCompleted.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        await initializedEvent.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        await SendBreakpointsAsync(breakpoints);
        await SendAsync("configurationDone", new { });
        await configurationCompleted.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;
        try { await SendAsync("disconnect", new { terminateDebuggee = true, restart = false }); }
        catch { }
        try
        {
            if (adapter is not null)
                await adapter.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
        }
        catch (TimeoutException)
        {
            try { adapter?.Kill(entireProcessTree: true); } catch { }
        }
        catch (InvalidOperationException) { }
    }

    public Task ContinueAsync() => SendAsync("continue", new { threadId = stoppedThreadId });
    public Task StepOverAsync() => SendAsync("next", new { threadId = stoppedThreadId, granularity = "line" });
    public Task StepIntoAsync() => SendAsync("stepIn", new { threadId = stoppedThreadId, granularity = "line" });
    public Task StepOutAsync() => SendAsync("stepOut", new { threadId = stoppedThreadId, granularity = "line" });

    private async Task SendBreakpointsAsync(IEnumerable<DebugBreakpoint> breakpoints)
    {
        foreach (var group in breakpoints.GroupBy(item => Path.GetFullPath(item.FilePath), StringComparer.OrdinalIgnoreCase))
        {
            var sourcePath = group.Key;
            var checksums = File.Exists(sourcePath)
                ? new[] { new { algorithm = "SHA256", checksum = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath))).ToLowerInvariant() } }
                : Array.Empty<object>();
            await SendAsync("setBreakpoints", new
            {
                source = new { path = sourcePath, name = Path.GetFileName(sourcePath), checksums },
                lines = group.Select(item => item.Line).ToArray(),
                breakpoints = group.Select(item => new { line = item.Line, column = 1 }).ToArray(),
                sourceModified = false
            });
        }
    }

    private async Task SendAsync(string command, object arguments)
    {
        if (input is null) throw new InvalidOperationException("Le débogueur n’est pas démarré.");
        var payload = JsonSerializer.Serialize(new { seq = ++sequence, type = "request", command, arguments });
        var bytes = Encoding.UTF8.GetByteCount(payload);
        await writeLock.WaitAsync();
        try
        {
            await input.WriteAsync($"Content-Length: {bytes}\r\n\r\n{payload}");
            await input.FlushAsync();
        }
        finally { writeLock.Release(); }
    }

    private async Task SendResponseAsync(int requestSequence, string command, string? signature = null)
    {
        if (input is null) throw new InvalidOperationException("Le débogueur n’est pas démarré.");
        object body = signature is null ? new Dictionary<string, string>() : new { signature };
        var payload = JsonSerializer.Serialize(new
        {
            seq = ++sequence,
            type = "response",
            request_seq = requestSequence,
            success = true,
            command,
            body
        });
        var bytes = Encoding.UTF8.GetByteCount(payload);
        await writeLock.WaitAsync();
        try
        {
            await input.WriteAsync($"Content-Length: {bytes}\r\n\r\n{payload}");
            await input.FlushAsync();
        }
        finally { writeLock.Release(); }
    }

    private async Task ReadMessagesAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var json = await DapMessageReader.ReadMessageAsync(stream, cancellationToken);
                if (json is null) break;
                OutputReceived?.Invoke(json + "\r\n");
                await HandleMessageAsync(json);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    private async Task HandleMessageAsync(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.TryGetProperty("type", out var incomingType) &&
                incomingType.GetString() == "request" &&
                root.TryGetProperty("command", out var incomingCommand) &&
                string.Equals(incomingCommand.GetString(), "handshake", StringComparison.OrdinalIgnoreCase))
            {
                var challenge = root.GetProperty("arguments").GetProperty("value").GetString() ?? string.Empty;
                var signature = await Task.Run(() => SignHandshake(challenge));
                await SendResponseAsync(root.GetProperty("seq").GetInt32(), "handshake", signature);
                handshakeCompleted?.TrySetResult(true);
                return;
            }
            if (root.TryGetProperty("event", out var eventName) && eventName.GetString() == "stopped")
            {
                stoppedThreadId = root.GetProperty("body").GetProperty("threadId").GetInt32();
                await SendAsync("stackTrace", new { threadId = stoppedThreadId, levels = 20 });
                return;
            }
            if (root.TryGetProperty("event", out eventName) && eventName.GetString() == "module" && activeBreakpoints.Count > 0)
            {
                var module = root.GetProperty("body").GetProperty("module");
                var modulePath = module.TryGetProperty("path", out var pathValue) ? pathValue.GetString() : null;
                if (modulePath is not null && debugProgramPath is not null &&
                    string.Equals(Path.GetFullPath(modulePath), debugProgramPath, StringComparison.OrdinalIgnoreCase))
                    await SendBreakpointsAsync(activeBreakpoints);
                return;
            }
            if (root.TryGetProperty("event", out eventName) && eventName.GetString() == "initialized")
            {
                initializedEvent?.TrySetResult(true);
                return;
            }
            if (!root.TryGetProperty("type", out var type) || type.GetString() != "response" || !root.TryGetProperty("command", out var command)) return;
            var commandName = command.GetString();
            if (commandName == "configurationDone")
            {
                if (root.TryGetProperty("success", out var success) && success.GetBoolean())
                    configurationCompleted?.TrySetResult(true);
                else
                {
                    var message = root.TryGetProperty("message", out var error) ? error.GetString() : "L’adaptateur a refusé la configuration.";
                    configurationCompleted?.TrySetException(new InvalidOperationException(message));
                }
                return;
            }
            if (commandName == "initialize")
            {
                if (root.TryGetProperty("success", out var success) && success.GetBoolean())
                    initializeCompleted?.TrySetResult(true);
                else
                    initializeCompleted?.TrySetException(new InvalidOperationException("L’adaptateur n’a pas accepté l’initialisation."));
                return;
            }
            if (commandName == "stackTrace")
            {
                currentFrames.Clear();
                foreach (var frame in root.GetProperty("body").GetProperty("stackFrames").EnumerateArray())
                {
                    var source = frame.TryGetProperty("source", out var sourceValue) && sourceValue.TryGetProperty("path", out var path) ? path.GetString() : null;
                    currentFrames.Add(new DebugFrame(frame.GetProperty("name").GetString() ?? "?", source, frame.GetProperty("line").GetInt32(), frame.GetProperty("column").GetInt32(), frame.GetProperty("id").GetInt32()));
                }
                if (currentFrames.Count > 0)
                    await SendAsync("scopes", new { frameId = currentFrames[0].Id });
                else SnapshotUpdated?.Invoke(new DebugSnapshot(stoppedThreadId, currentFrames.ToArray(), []));
                return;
            }
            if (commandName == "scopes")
            {
                var localScope = root.GetProperty("body").GetProperty("scopes").EnumerateArray()
                    .FirstOrDefault(scope => string.Equals(scope.GetProperty("name").GetString(), "Locals", StringComparison.OrdinalIgnoreCase));
                if (localScope.ValueKind != JsonValueKind.Undefined)
                    await SendAsync("variables", new { variablesReference = localScope.GetProperty("variablesReference").GetInt32() });
                else SnapshotUpdated?.Invoke(new DebugSnapshot(stoppedThreadId, currentFrames.ToArray(), []));
                return;
            }
            if (commandName == "variables")
            {
                var variables = root.GetProperty("body").GetProperty("variables").EnumerateArray()
                    .Select(variable => new DebugVariable(variable.GetProperty("name").GetString() ?? "?",
                        variable.TryGetProperty("value", out var value) ? value.GetString() ?? value.ToString() : "",
                        variable.TryGetProperty("type", out var variableType) ? variableType.GetString() : null))
                    .ToArray();
                SnapshotUpdated?.Invoke(new DebugSnapshot(stoppedThreadId, currentFrames.ToArray(), variables));
            }
        }
        catch (JsonException) { }
        catch (KeyNotFoundException) { }
        catch (InvalidOperationException) { }
    }

    private async Task ReadErrorsAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        try { while (!cancellationToken.IsCancellationRequested && await reader.ReadLineAsync(cancellationToken) is { } line) OutputReceived?.Invoke(line + "\r\n"); }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    private static string? SignHandshake(string challenge)
    {
        if (challenge.Length == 0) return null;
        var vsda = FindVsdaModule();
        var node = FindNode();
        if (vsda is null || node is null) return null;

        const string script = "const vsda=require(process.argv[1]); const signer=new vsda.signer(); process.stdout.write(signer.sign(process.argv[2]));";
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(node)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.StartInfo.ArgumentList.Add("-e");
            process.StartInfo.ArgumentList.Add(script);
            process.StartInfo.ArgumentList.Add(vsda);
            process.StartInfo.ArgumentList.Add(challenge);
            if (!process.Start()) return null;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch (Exception) { return null; }
    }

    private static string? FindNode()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("KEYCE_NODE_PATH"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe"),
            "node"
        };
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            if (Path.IsPathFullyQualified(candidate) && !File.Exists(candidate)) continue;
            if (candidate == "node" || File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static string? FindVsdaModule()
    {
        var codeRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code");
        if (!Directory.Exists(codeRoot)) return null;
        return Directory.EnumerateFiles(codeRoot, "vsda.node", SearchOption.AllDirectories).FirstOrDefault();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        input?.Dispose();
        adapter?.Dispose();
    }
}
