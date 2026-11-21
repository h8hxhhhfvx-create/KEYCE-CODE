using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace KeyceCode.ViewModels;

// NB : si un autre membre de l'équipe a déjà une classe de base MVVM / RelayCommand,
// supprimez ce fichier et utilisez les leurs (adaptez juste les noms).

public abstract class ViewModelBase : INotifyPropertyChanged
{
    // Capturé à la création du ViewModel (thread UI) pour pouvoir y revenir depuis un thread secondaire.
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    /// <summary>Exécute l'action sur le thread UI (les services lèvent leurs événements en arrière-plan).</summary>
    protected void PostToUi(Action action)
    {
        if (_ui is null) action();
        else _ui.Post(_ => action(), null);
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = _ => execute();
        _canExecute = canExecute is null ? null : _ => canExecute();
    }

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>Force WPF à réévaluer l'état activé/désactivé des boutons.</summary>
    public static void Refresh() => CommandManager.InvalidateRequerySuggested();
}
