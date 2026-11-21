using System.Collections.ObjectModel;
using System.ComponentModel;

namespace KeyceCode.Models;

public enum OutputKind { StdOut, StdErr, Info, Success, Error, Input }

/// <summary>Une ligne affichée dans la console (sa couleur dépend de Kind).</summary>
public sealed class ConsoleLine : INotifyPropertyChanged
{
    private string _text;

    public ConsoleLine(OutputKind kind, string text)
    {
        Kind = kind;
        _text = text;
    }

    public OutputKind Kind { get; }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Transforme des morceaux de texte (pas forcément des lignes complètes) en lignes de console.
/// Un prompt comme "Entrez un nombre : " reste "ouvert" jusqu'à la saisie de l'utilisateur.
/// À appeler uniquement depuis le thread UI.
/// </summary>
public sealed class ConsoleBuffer
{
    private const int MaxLines = 5000;
    private ConsoleLine? _open;

    public ObservableCollection<ConsoleLine> Lines { get; } = new();

    public void Append(string chunk, OutputKind kind)
    {
        if (string.IsNullOrEmpty(chunk)) return;

        var parts = chunk.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < parts.Length; i++)
        {
            bool endsLine = i < parts.Length - 1;
            string part = parts[i];
            if (part.Length == 0 && !endsLine) continue;

            if (_open == null || _open.Kind != kind)
            {
                _open = new ConsoleLine(kind, "");
                Lines.Add(_open);
            }

            _open.Text += part;
            if (endsLine) _open = null;
        }

        Trim();
    }

    public void AppendLine(string text, OutputKind kind) => Append(text + "\n", kind);

    /// <summary>Affiche ce que l'utilisateur a tapé (à la suite du prompt s'il y en a un).</summary>
    public void AppendInput(string text)
    {
        if (_open != null)
        {
            _open.Text += text;
            _open = null;
        }
        else
        {
            Lines.Add(new ConsoleLine(OutputKind.Input, text));
        }

        Trim();
    }

    public void Clear()
    {
        Lines.Clear();
        _open = null;
    }

    private void Trim()
    {
        while (Lines.Count > MaxLines) Lines.RemoveAt(0);
    }
}
