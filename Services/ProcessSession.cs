using System.Diagnostics;
using System.Globalization;
using System.Text;
using KeyceCode.Models;

namespace KeyceCode.Services;

/// <summary>
/// Lance un processus, lit sa sortie (stdout/stderr) au fil de l'eau — même sans retour à la ligne,
/// pour voir les prompts — et permet d'écrire dans son entrée standard ou de le tuer.
/// Utilisé par CompilerService et TerminalService.
/// </summary>
internal sealed class ProcessSession : IDisposable
{
    private readonly Process _process;
    private volatile bool _killed;

    public event Action<string, OutputKind>? OutputReceived;

    public bool IsRunning { get; private set; }
    public bool WasKilled => _killed;

    static ProcessSession()
    {
        // Permet de lire les pages de code Windows (ex. 850) pour bien afficher les accents.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public ProcessSession(string fileName, string arguments, string workingDirectory)
    {
        var encoding = GetConsoleEncoding();

        _process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments)
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

        _process.StartInfo.Environment["DOTNET_NOLOGO"] = "1";
        _process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        _process.StartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
    }

    /// <summary>Démarre le processus et attend sa fin. Renvoie le code de sortie.</summary>
    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        _process.Start();
        IsRunning = true;
        using var registration = ct.Register(Kill);

        var pumps = Task.WhenAll(
            PumpAsync(_process.StandardOutput, OutputKind.StdOut),
            PumpAsync(_process.StandardError, OutputKind.StdErr));

        try
        {
            await _process.WaitForExitAsync();
            // Laisse 2 s pour vider les derniers caractères des flux.
            await Task.WhenAny(pumps, Task.Delay(2000));
            return _process.ExitCode;
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>Envoie du texte dans l'entrée standard (saisie clavier du programme).</summary>
    public void Write(string text)
    {
        try
        {
            if (IsRunning) _process.StandardInput.Write(text);
        }
        catch (IOException) { }
        catch (InvalidOperationException) { }
    }

    /// <summary>Arrête le processus ET tous ses sous-processus.</summary>
    public void Kill()
    {
        _killed = true;
        try
        {
            if (IsRunning && !_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch { /* déjà terminé */ }
    }

    public void Dispose() => _process.Dispose();

    private async Task PumpAsync(StreamReader reader, OutputKind kind)
    {
        var buffer = new char[1024];
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                OutputReceived?.Invoke(new string(buffer, 0, read), kind);
        }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
    }

    private static Encoding GetConsoleEncoding()
    {
        try
        {
            return Encoding.GetEncoding(CultureInfo.InstalledUICulture.TextInfo.OEMCodePage);
        }
        catch
        {
            return new UTF8Encoding(false);
        }
    }
}
