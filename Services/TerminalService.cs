using System.ComponentModel;
using System.Text.RegularExpressions;
using KeyceCode.Models;

namespace KeyceCode.Services;

/// <summary>
/// Terminal intégré : exécute chaque commande via cmd.exe dans le dossier courant.
/// Les commandes cd / cls et le changement de lecteur (D:) sont gérés ici,
/// car ils doivent modifier l'état du terminal et pas seulement d'un processus.
/// Pendant qu'une commande tourne, SendInput() écrit dans son entrée standard.
/// </summary>
public sealed class TerminalService
{
    private static readonly Regex CdRegex =
        new(@"^(?:cd|chdir)(?:\s+/d)?(?:\s+|(?=[.\\/])|$)(?<arg>.*)$", RegexOptions.IgnoreCase);

    private static readonly Regex DriveRegex = new(@"^[A-Za-z]:$");

    private volatile bool _busy;
    private ProcessSession? _current;

    public TerminalService(string? initialDirectory = null)
    {
        WorkingDirectory = Directory.Exists(initialDirectory)
            ? Path.GetFullPath(initialDirectory!)
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    public string WorkingDirectory { get; private set; }

    public bool IsRunning => _busy;

    public event Action<string, OutputKind>? OutputReceived;
    public event Action? DirectoryChanged;
    public event Action? ClearRequested;

    /// <summary>Change le dossier courant (ex. quand l'utilisateur ouvre un projet).</summary>
    public void SetWorkingDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        WorkingDirectory = Path.GetFullPath(path);
        DirectoryChanged?.Invoke();
    }

    /// <summary>Exécute une ligne de commande et renvoie son code de sortie.</summary>
    public async Task<int> ExecuteAsync(string commandLine)
    {
        commandLine = commandLine.Trim();
        if (commandLine.Length == 0) return 0;

        if (_busy)
            throw new InvalidOperationException("Une commande est déjà en cours d'exécution.");

        if (TryHandleBuiltIn(commandLine, out int builtInCode))
            return builtInCode;

        _busy = true;
        using var session = new ProcessSession("cmd.exe", $"/d /s /c \"{commandLine}\"", WorkingDirectory);
        session.OutputReceived += (text, kind) => OutputReceived?.Invoke(text, kind);
        _current = session;

        try
        {
            return await session.RunAsync();
        }
        catch (Win32Exception ex)
        {
            OutputReceived?.Invoke("Impossible de lancer la commande : " + ex.Message + "\n", OutputKind.Error);
            return -1;
        }
        finally
        {
            _current = null;
            _busy = false;
        }
    }

    /// <summary>Envoie une ligne au programme en cours (saisie clavier).</summary>
    public void SendInput(string text) => _current?.Write(text + Environment.NewLine);

    /// <summary>Arrête la commande en cours (équivalent de Ctrl+C).</summary>
    public void Stop() => _current?.Kill();

    // ------------------------------------------------------------------

    private bool TryHandleBuiltIn(string cmd, out int exitCode)
    {
        exitCode = 0;

        // Commandes composées (a && b, a | b, a > f) : on laisse cmd.exe s'en charger.
        if (cmd.IndexOfAny(new[] { '&', '|', '<', '>' }) >= 0) return false;

        if (cmd.Equals("cls", StringComparison.OrdinalIgnoreCase) ||
            cmd.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            ClearRequested?.Invoke();
            return true;
        }

        if (DriveRegex.IsMatch(cmd))
        {
            string root = cmd + "\\";
            if (Directory.Exists(root)) SetWorkingDirectory(root);
            else
            {
                OutputReceived?.Invoke("Le lecteur spécifié est introuvable.\n", OutputKind.Error);
                exitCode = 1;
            }
            return true;
        }

        var match = CdRegex.Match(cmd);
        if (!match.Success) return false;

        string arg = match.Groups["arg"].Value.Trim().Trim('"');
        if (arg.Length == 0)
        {
            OutputReceived?.Invoke(WorkingDirectory + "\n", OutputKind.StdOut);
            return true;
        }

        try
        {
            string target = Path.GetFullPath(arg, WorkingDirectory);
            if (Directory.Exists(target))
            {
                WorkingDirectory = target;
                DirectoryChanged?.Invoke();
            }
            else
            {
                OutputReceived?.Invoke("Le chemin d'accès spécifié est introuvable.\n", OutputKind.Error);
                exitCode = 1;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            OutputReceived?.Invoke("Chemin invalide : " + ex.Message + "\n", OutputKind.Error);
            exitCode = 1;
        }

        return true;
    }
}
