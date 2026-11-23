using KeyceCode.Models;

namespace KeyceCode.Views;

/// <summary>
/// Contrat utilisé par l’application pour manipuler un éditeur sans dépendre du contrôle AvalonEdit.
/// </summary>
internal interface ICodeEditorSurface
{
    event EventHandler? TextChanged;
    event EventHandler? CaretPositionChanged;
    string Text { get; set; }
    bool Modified { get; set; }
    bool IsReadOnly { get; set; }
    (int Line, int Column) CaretPosition { get; }
    int SelectionStart { get; set; }
    int SelectionLength { get; set; }
    void Select(int start, int length);
    void ApplyLanguage(string? extension);
    void SetDiagnostics(IEnumerable<Diagnostic> diagnostics);
    void ApplyAppearance(AppSettings settings);
    void Undo();
    void Redo();
    void Cut();
    void Copy();
    void Paste();
    void ReplaceText(string text);
    void SetExamMode(bool enabled);
    bool Focus();
    void ToggleLineComment();
}
