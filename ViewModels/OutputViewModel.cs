using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
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
    private readonly LearningHintService _learningHints = new();
    private string? _projectPath;
    private string _inputText = "";
    private string _status = "Prêt";
    private bool _isBusy;
    private bool _learningMode;
    private BuildDiagnostic? _selectedDiagnostic;
    private string _diagnosticFilter = "";
    private string _diagnosticSeverity = "Tous";
    private string _diagnosticSort = "Position";

    public OutputViewModel()
    {
        _compiler.OutputReceived += (text, kind) => PostToUi(() => Log.Append(text, kind));
        DiagnosticsView = CollectionViewSource.GetDefaultView(Diagnostics);
        DiagnosticsView.Filter = FilterDiagnostic;
        Diagnostics.CollectionChanged += (_, _) => OnPropertyChanged(nameof(VisibleDiagnosticCount));
        DiagnosticsView.CollectionChanged += (_, _) => OnPropertyChanged(nameof(VisibleDiagnosticCount));
        DiagnosticsView.SortDescriptions.Add(new SortDescription(nameof(BuildDiagnostic.File), ListSortDirection.Ascending));
        DiagnosticsView.SortDescriptions.Add(new SortDescription(nameof(BuildDiagnostic.Line), ListSortDirection.Ascending));
        DiagnosticsView.SortDescriptions.Add(new SortDescription(nameof(BuildDiagnostic.Column), ListSortDirection.Ascending));

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
    public ICollectionView DiagnosticsView { get; }
    public int VisibleDiagnosticCount => DiagnosticsView.Cast<BuildDiagnostic>().Count();

    public IReadOnlyList<string> DiagnosticSeverityOptions { get; } = ["Tous", "Erreurs", "Avertissements"];
    public IReadOnlyList<string> DiagnosticSortOptions { get; } = ["Position", "Gravité", "Fichier"];

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

    public string? ResolveRunnableProject(string path) => _compiler.ResolveRunnableProject(path);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value)) RelayCommand.Refresh();
        }
    }

    public bool LearningMode
    {
        get => _learningMode;
        set => SetProperty(ref _learningMode, value);
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

    public string DiagnosticFilter
    {
        get => _diagnosticFilter;
        set
        {
            if (!SetProperty(ref _diagnosticFilter, value)) return;
            DiagnosticsView.Refresh();
            OnPropertyChanged(nameof(VisibleDiagnosticCount));
        }
    }

    public string DiagnosticSeverity
    {
        get => _diagnosticSeverity;
        set
        {
            if (!SetProperty(ref _diagnosticSeverity, value)) return;
            DiagnosticsView.Refresh();
            OnPropertyChanged(nameof(VisibleDiagnosticCount));
        }
    }

    public string DiagnosticSort
    {
        get => _diagnosticSort;
        set
        {
            if (!SetProperty(ref _diagnosticSort, value)) return;
            DiagnosticsView.SortDescriptions.Clear();
            if (value == "Gravité")
                DiagnosticsView.SortDescriptions.Add(new SortDescription(nameof(BuildDiagnostic.Severity), ListSortDirection.Ascending));
            else if (value == "Fichier")
                DiagnosticsView.SortDescriptions.Add(new SortDescription(nameof(BuildDiagnostic.File), ListSortDirection.Ascending));
            else
            {
                DiagnosticsView.SortDescriptions.Add(new SortDescription(nameof(BuildDiagnostic.File), ListSortDirection.Ascending));
                DiagnosticsView.SortDescriptions.Add(new SortDescription(nameof(BuildDiagnostic.Line), ListSortDirection.Ascending));
                DiagnosticsView.SortDescriptions.Add(new SortDescription(nameof(BuildDiagnostic.Column), ListSortDirection.Ascending));
            }
            DiagnosticsView.Refresh();
            OnPropertyChanged(nameof(VisibleDiagnosticCount));
        }
    }

    /// <summary>À brancher sur l'éditeur : enregistrer tous les fichiers ouverts avant de compiler.</summary>
    public event Action? SaveAllRequested;

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

        SaveAllRequested?.Invoke();

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

            foreach (var d in result.Diagnostics)
            {
                Diagnostics.Add(d);
                if (LearningMode)
                    Log.AppendLine($"Piste pour {d.Code} : {_learningHints.GetHint(d.Code)}", OutputKind.Info);
            }
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

    private bool FilterDiagnostic(object item)
    {
        if (item is not BuildDiagnostic diagnostic) return false;
        if (DiagnosticSeverity == "Erreurs" && !diagnostic.IsError) return false;
        if (DiagnosticSeverity == "Avertissements" && diagnostic.IsError) return false;
        if (string.IsNullOrWhiteSpace(DiagnosticFilter)) return true;

        var query = DiagnosticFilter.Trim();
        return diagnostic.Display.Contains(query, StringComparison.OrdinalIgnoreCase)
            || diagnostic.Code.Contains(query, StringComparison.OrdinalIgnoreCase)
            || diagnostic.Message.Contains(query, StringComparison.OrdinalIgnoreCase)
            || diagnostic.File.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
