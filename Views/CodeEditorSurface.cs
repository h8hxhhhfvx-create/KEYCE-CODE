using System.Windows.Forms.Integration;
using System.ComponentModel;

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
    }

    public new event EventHandler? TextChanged;

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
    public void SelectLine(int line, int column = 1) => editorView.SelectLine(line, column);
    public void ApplyLanguage(string? extension) => editorView.ApplyLanguage(extension);
    public void SetTheme(bool dark) => editorView.SetTheme(dark);
    public void Undo() => editorView.Undo();
    public void Redo() => editorView.Redo();
    public void Cut() => editorView.Cut();
    public void Copy() => editorView.Copy();
    public void Paste() => editorView.Paste();
}
