using KeyceCode.Models;
using KeyceCode.Services;

namespace KeyceCode.ViewModels;

/// <summary>Terminal intégré : saisie de commandes, historique (↑/↓), programmes interactifs.</summary>
public sealed class TerminalViewModel : ViewModelBase
{
    private readonly TerminalService _terminal;
    private readonly List<string> _history = new();
    private int _historyIndex;
    private string _inputText = "";
    private bool _isBusy;

    public TerminalViewModel(string? initialDirectory = null)
    {
        _terminal = new TerminalService(initialDirectory);
        _terminal.OutputReceived += (text, kind) => PostToUi(() => Log.Append(text, kind));
        _terminal.DirectoryChanged += () => PostToUi(() => OnPropertyChanged(nameof(Prompt)));
        _terminal.ClearRequested += () => PostToUi(Log.Clear);

        SubmitCommand = new RelayCommand(Submit);
        StopCommand = new RelayCommand(Stop, () => IsBusy);
        ClearCommand = new RelayCommand(Log.Clear);
        HistoryUpCommand = new RelayCommand(HistoryUp);
        HistoryDownCommand = new RelayCommand(HistoryDown);

        Log.AppendLine("Terminal KEYCE CODE — tapez une commande (ex. dotnet --version).", OutputKind.Info);
    }

    public ConsoleBuffer Log { get; } = new();

    public RelayCommand SubmitCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand HistoryUpCommand { get; }
    public RelayCommand HistoryDownCommand { get; }

    /// <summary>Texte affiché avant la zone de saisie : dossier courant, ou "›" si un programme attend une saisie.</summary>
    public string Prompt => IsBusy ? "›" : _terminal.WorkingDirectory + ">";

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            OnPropertyChanged(nameof(Prompt));
            RelayCommand.Refresh();
        }
    }

    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value);
    }

    /// <summary>Place le terminal dans le dossier du projet ouvert.</summary>
    public void SetWorkingDirectory(string path) => _terminal.SetWorkingDirectory(path);

    /// <summary>Arrête la commande en cours (à appeler aussi à la fermeture de l'application).</summary>
    public void Stop()
    {
        if (IsBusy) _terminal.Stop();
    }

    private void Submit()
    {
        string text = InputText;
        InputText = "";

        // Un programme tourne : ce que l'utilisateur tape est sa saisie clavier.
        if (IsBusy)
        {
            Log.AppendInput(text);
            _terminal.SendInput(text);
            return;
        }

        if (string.IsNullOrWhiteSpace(text)) return;

        _history.Add(text);
        _historyIndex = _history.Count;
        Log.AppendLine($"{_terminal.WorkingDirectory}> {text}", OutputKind.Input);
        _ = ExecuteAsync(text);
    }

    private async Task ExecuteAsync(string text)
    {
        IsBusy = true;
        try
        {
            int code = await _terminal.ExecuteAsync(text);
            if (code != 0) Log.AppendLine($"(code de sortie : {code})", OutputKind.Info);
        }
        catch (Exception ex)
        {
            Log.AppendLine(ex.Message, OutputKind.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void HistoryUp()
    {
        if (_historyIndex <= 0) return;
        _historyIndex--;
        InputText = _history[_historyIndex];
    }

    private void HistoryDown()
    {
        if (_historyIndex < _history.Count - 1)
        {
            _historyIndex++;
            InputText = _history[_historyIndex];
        }
        else
        {
            _historyIndex = _history.Count;
            InputText = "";
        }
    }
}
