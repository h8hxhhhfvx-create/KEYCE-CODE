using System.Collections.ObjectModel;
using KeyceCode.Models;
using KeyceCode.Services;

namespace KeyceCode.ViewModels;

/// <summary>
/// Console de sortie : boutons Compiler / Exécuter / Arrêter, affichage des résultats,
/// des erreurs de compilation et saisie clavier pour les programmes interactifs.
/// </summary>
public sealed class OutputViewModel : ViewModelBase
{
    private readonly CompilerService _compiler = new();
    private string? _projectPath;
    private string _inputText = "";
    private string _status = "Prêt";
    private bool _isBusy;
    private BuildDiagnostic? _selectedDiagnostic;

    public OutputViewModel()
    {
        _compiler.OutputReceived += (text, kind) => PostToUi(() => Log.Append(text, kind));

        CompileCommand = new RelayCommand(() => _ = RunOperationAsync(run: false), () => !IsBusy);
        RunCommand = new RelayCommand(() => _ = RunOperationAsync(run: true), () => !IsBusy);
        StopCommand = new RelayCommand(Stop, () => IsBusy);
        ClearCommand = new RelayCommand(() => { Log.Clear(); Diagnostics.Clear(); });
        SendInputCommand = new RelayCommand(SendInput, () => IsBusy);
        OpenDiagnosticCommand = new RelayCommand(p =>
        {
            if (p is BuildDiagnostic d) DiagnosticActivated?.Invoke(d);
        });
    }

    public ConsoleBuffer Log { get; } = new();
    public ObservableCollection<BuildDiagnostic> Diagnostics { get; } = new();

    public RelayCommand CompileCommand { get; }
    public RelayCommand RunCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand SendInputCommand { get; }
    public RelayCommand OpenDiagnosticCommand { get; }

    /// <summary>Dossier du projet, fichier .csproj ou fichier .cs à compiler (renseigné par la fenêtre principale).</summary>
    public string? ProjectPath
    {
        get => _projectPath;
        set => SetProperty(ref _projectPath, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value)) RelayCommand.Refresh();
        }
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value);
    }

    public BuildDiagnostic? SelectedDiagnostic
    {
        get => _selectedDiagnostic;
        set => SetProperty(ref _selectedDiagnostic, value);
    }

    /// <summary>À brancher sur l'éditeur : enregistrer tous les fichiers ouverts avant de compiler.</summary>
    public event Func<bool>? SaveAllRequested;

    /// <summary>Double-clic sur une erreur : l'éditeur peut ouvrir le fichier et aller à la ligne.</summary>
    public event Action<BuildDiagnostic>? DiagnosticActivated;

    /// <summary>Arrête le programme en cours (à appeler aussi à la fermeture de l'application).</summary>
    public void Stop()
    {
        if (IsBusy) _compiler.Stop();
    }

    private async Task RunOperationAsync(bool run)
    {
        if (string.IsNullOrWhiteSpace(ProjectPath))
        {
            Log.AppendLine("Aucun projet ouvert : ouvrez un dossier contenant un .csproj ou un fichier .cs.", OutputKind.Error);
            return;
        }

        if (SaveAllRequested?.Invoke() == false)
        {
            Status = "Opération annulée";
            return;
        }

        Log.Clear();
        Diagnostics.Clear();
        IsBusy = true;
        Status = run ? "Exécution en cours…" : "Compilation en cours…";
        Log.AppendLine(run ? $"▶ Exécution : {ProjectPath}" : $"🔨 Compilation : {ProjectPath}", OutputKind.Info);

        try
        {
            ExecutionResult result = run
                ? await _compiler.RunAsync(ProjectPath)
                : await _compiler.BuildAsync(ProjectPath);

            foreach (var d in result.Diagnostics) Diagnostics.Add(d);
            int errors = result.Diagnostics.Count(d => d.IsError);

            if (result.WasStopped)
            {
                Log.AppendLine("■ Arrêté par l'utilisateur.", OutputKind.Info);
                Status = "Arrêté";
            }
            else if (errors > 0)
            {
                Log.AppendLine($"✘ Échec de la compilation : {errors} erreur(s).", OutputKind.Error);
                Status = $"{errors} erreur(s)";
            }
            else if (!run)
            {
                if (result.Success)
                {
                    Log.AppendLine("✔ Compilation réussie.", OutputKind.Success);
                    Status = "Compilation réussie";
                }
                else
                {
                    Log.AppendLine($"✘ La compilation a échoué (code {result.ExitCode}).", OutputKind.Error);
                    Status = "Échec";
                }
            }
            else
            {
                Log.AppendLine(
                    $"Programme terminé avec le code {result.ExitCode}.",
                    result.ExitCode == 0 ? OutputKind.Success : OutputKind.Error);
                Status = $"Terminé (code {result.ExitCode})";
            }
        }
        catch (Exception ex)
        {
            Log.AppendLine(ex.Message, OutputKind.Error);
            Status = "Erreur";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SendInput()
    {
        string text = InputText;
        InputText = "";
        Log.AppendInput(text);
        _compiler.SendInput(text);
    }
}
