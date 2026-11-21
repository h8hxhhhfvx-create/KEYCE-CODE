using System.Windows.Forms.Integration;
using System.ComponentModel;
using KeyceCode.Models;

namespace KeyceCode.Views;

internal sealed class CodeEditorSurface : UserControl
{
    private readonly ElementHost host = new() { Dock = DockStyle.Fill };
    private readonly EditorView editorView = new();

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
    public void ApplyAppearance(AppSettings settings) => editorView.ApplyAppearance(settings);
    public void Undo() => editorView.Undo();
    public void Redo() => editorView.Redo();
    public void Cut() => editorView.Cut();
    public void Copy() => editorView.Copy();
    public void Paste() => editorView.Paste();
    public void ReplaceText(string text) => editorView.ReplaceText(text);
}
