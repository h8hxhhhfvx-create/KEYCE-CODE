using System.Diagnostics;
using System.Globalization;
using System.Text;
using KeyceCode.Models;

namespace KeyceCode.Services;

/// <summary>
/// Session cmd.exe ou PowerShell persistante utilisée par le terminal intégré.
/// Chaque commande est suivie d'un marqueur interne afin de connaître son code
/// de sortie sans recréer un shell et sans perdre son état (cd, set, etc.).
/// </summary>
internal sealed class InteractiveShellSession : IDisposable
{
    private const string ExitMarker = "__KEYCE_CODE_EXIT_";
    private readonly Process process;
    private readonly string exitCommand;
    private readonly Encoding encoding;
    private readonly SemaphoreSlim commandGate = new(1, 1);
    private readonly StringBuilder pendingOutput = new();
    private TaskCompletionSource<int>? commandCompletion;
    private bool disposed;

    public event Action<string, OutputKind>? OutputReceived;

    public bool IsRunning => !disposed && !process.HasExited;

    public InteractiveShellSession(string executable, string arguments, string workingDirectory, bool powerShell)
    {
        encoding = GetConsoleEncoding();
        exitCommand = powerShell
            ? "Write-Output (\"{0}\" + $(if ($?) {{ 0 }} else {{ 1 }}))"
            : "echo {0}!errorlevel!";
        process = new Process
        {
            StartInfo = new ProcessStartInfo(executable, arguments)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = encoding,
                StandardOutputEncoding = encoding,
                StandardErrorEncoding = encoding
            }
        };
        process.StartInfo.Environment["DOTNET_NOLOGO"] = "1";
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        process.Start();
        _ = PumpOutputAsync(process.StandardOutput, OutputKind.StdOut);
        _ = PumpOutputAsync(process.StandardError, OutputKind.StdErr);
    }

    public async Task<int> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        await commandGate.WaitAsync(cancellationToken);
        try
        {
            if (!IsRunning) return -1;
            pendingOutput.Clear();
            commandCompletion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var marker = ExitMarker + Guid.NewGuid().ToString("N") + "_";
            // Le marqueur doit rester dans la ligne de commande du shell. Si on
            // l'envoie comme une deuxième ligne, un programme interactif (Read-Host,
            // input(), set /p, etc.) le consomme comme une saisie utilisateur.
            var completionCommand = string.Equals(exitCommand, "echo {0}!errorlevel!", StringComparison.Ordinal)
                ? $"{command} & {string.Format(exitCommand, marker)}"
                : $"{command}; {string.Format(exitCommand, marker)}";
            process.StandardInput.WriteLine(completionCommand);
            await process.StandardInput.FlushAsync(cancellationToken);

            using var registration = cancellationToken.Register(() => commandCompletion.TrySetCanceled(cancellationToken));
            return await commandCompletion.Task;
        }
        finally
        {
            commandCompletion = null;
            commandGate.Release();
        }
    }

    public void Write(string text)
    {
        try
        {
            if (IsRunning)
            {
                process.StandardInput.Write(text);
                process.StandardInput.Flush();
            }
        }
        catch (IOException) { }
        catch (InvalidOperationException) { }
    }

    public void Kill()
    {
        try
        {
            if (IsRunning) process.Kill(entireProcessTree: true);
        }
        catch { /* le processus est déjà terminé */ }
        commandCompletion?.TrySetResult(-1);
    }

    private async Task PumpOutputAsync(StreamReader reader, OutputKind kind)
    {
        var buffer = new char[1024];
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                if (kind == OutputKind.StdOut)
                    ProcessStdOut(new string(buffer, 0, read));
                else
                    OutputReceived?.Invoke(new string(buffer, 0, read), kind);
            }
        }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
    }

    private void ProcessStdOut(string chunk)
    {
        pendingOutput.Append(chunk);
        while (true)
        {
            var markerStart = pendingOutput.ToString().IndexOf(ExitMarker, StringComparison.Ordinal);
            if (markerStart < 0)
            {
                if (pendingOutput.Length > ExitMarker.Length)
                {
                    var safeLength = pendingOutput.Length - ExitMarker.Length;
                    var safe = pendingOutput.ToString(0, safeLength);
                    pendingOutput.Remove(0, safeLength);
                    OutputReceived?.Invoke(safe, OutputKind.StdOut);
                }
                return;
            }

            if (markerStart > 0)
            {
                OutputReceived?.Invoke(pendingOutput.ToString(0, markerStart), OutputKind.StdOut);
                pendingOutput.Remove(0, markerStart);
            }

            var newline = FindNewline(pendingOutput);
            if (newline < 0) return;
            var markerLine = pendingOutput.ToString(0, newline).Trim();
            pendingOutput.Remove(0, newline + 1);
            var lastSeparator = markerLine.LastIndexOf('_');
            var codeText = lastSeparator >= 0 ? markerLine[(lastSeparator + 1)..].Trim() : string.Empty;
            if (int.TryParse(codeText, out var code)) commandCompletion?.TrySetResult(code);
        }
    }

    private static int FindNewline(StringBuilder value)
    {
        for (var index = 0; index < value.Length; index++)
            if (value[index] is '\r' or '\n') return index;
        return -1;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Kill();
        process.Dispose();
        commandGate.Dispose();
    }

    private static Encoding GetConsoleEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try { return Encoding.GetEncoding(CultureInfo.InstalledUICulture.TextInfo.OEMCodePage); }
        catch { return new UTF8Encoding(false); }
    }
}
