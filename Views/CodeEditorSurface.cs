using System.Windows.Forms.Integration;
using System.ComponentModel;
using KeyceCode.Models;

namespace KeyceCode.Views;

internal sealed class CodeEditorSurface : UserControl, ICodeEditorSurface
{
    private readonly ElementHost host = new() { Dock = DockStyle.Fill };
    private readonly EditorView editorView = new();
    private bool examMode;

    public CodeEditorSurface()
    {
        Dock = DockStyle.Fill;
        host.Child = editorView;
        Controls.Add(host);
        editorView.ContentChanged += (_, _) => TextChanged?.Invoke(this, EventArgs.Empty);
        editorView.CaretPositionChanged += (_, _) => CaretPositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public new event EventHandler? TextChanged;
    public event EventHandler? CaretPositionChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new string Text
    {
        get => editorView.Text;
        set => editorView.Text = value;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Modified
    {
        get => editorView.IsModified;
        set => editorView.IsModified = value;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsReadOnly
    {
        get => editorView.IsReadOnly;
        set => editorView.IsReadOnly = value;
    }

    public (int Line, int Column) CaretPosition => editorView.CaretPosition;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectionStart
    {
        get => editorView.SelectionStart;
        set => editorView.SelectionStart = value;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectionLength
    {
        get => editorView.SelectionLength;
        set => editorView.SelectionLength = value;
    }

    public void Select(int start, int length) => editorView.Select(start, length);
    public void ApplyLanguage(string? extension) => editorView.ApplyLanguage(extension);
    public void SetTheme(bool dark) => editorView.SetTheme(dark);
    public void SetDiagnostics(IEnumerable<Diagnostic> diagnostics) => editorView.SetDiagnostics(diagnostics);
    public void ApplyAppearance(AppSettings settings) => editorView.ApplyAppearance(settings);
    public void Undo() => editorView.Undo();
    public void Redo() => editorView.Redo();
    public void Cut() => editorView.Cut();
    public void Copy() => editorView.Copy();
    public void Paste() => editorView.Paste();
    public void ReplaceText(string text) => editorView.ReplaceText(text);
    public void ToggleLineComment() => editorView.ToggleLineComment();

    public void SetExamMode(bool enabled)
    {
        examMode = enabled;
        editorView.SetExamMode(enabled);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (examMode && (keyData == (Keys.Control | Keys.C) || keyData == (Keys.Control | Keys.X) || keyData == (Keys.Control | Keys.V)))
            return true;
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
