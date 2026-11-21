using System.Windows;
using ICSharpCode.AvalonEdit.Document;
using KeyceCode.Services;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace KeyceCode.Views;

public partial class EditorView : WpfUserControl
{
    private const int IndentSize = 4;
    private readonly SyntaxHighlightingService highlightingService = new();
    private string savedText = string.Empty;
    private bool suppressChanges;

    public EditorView()
    {
        InitializeComponent();
        highlightingService.Apply(Editor);
        Editor.TextArea.TextEntering += OnTextEntering;
        Editor.TextArea.TextEntered += OnTextEntered;
        Editor.TextChanged += OnTextChanged;
        UpdateMiniMap();
    }

    public event EventHandler? ContentChanged;

    public string Text
    {
        get => Editor.Text;
        set
        {
            suppressChanges = true;
            Editor.Text = value ?? string.Empty;
            Editor.Document.UndoStack.ClearAll();
            savedText = Editor.Text;
            UpdateMiniMap();
            suppressChanges = false;
        }
    }

    public bool IsModified
    {
        get => !string.Equals(Editor.Text, savedText, StringComparison.Ordinal);
        set
        {
            if (!value) savedText = Editor.Text;
        }
    }

    public int SelectionStart
    {
        get => Editor.SelectionStart;
        set => Editor.SelectionStart = Math.Clamp(value, 0, Editor.Text.Length);
    }

    public int SelectionLength
    {
        get => Editor.SelectionLength;
        set => Editor.SelectionLength = Math.Clamp(value, 0, Editor.Text.Length - Editor.SelectionStart);
    }

    public void Select(int start, int length)
    {
        Editor.Select(Math.Clamp(start, 0, Editor.Text.Length),
            Math.Clamp(length, 0, Editor.Text.Length - Math.Clamp(start, 0, Editor.Text.Length)));
        Editor.TextArea.Caret.BringCaretToView();
        Editor.Focus();
    }

    public void Undo() => Editor.Undo();
    public void Redo() => Editor.Redo();
    public void Cut() => Editor.Cut();
    public void Copy() => Editor.Copy();
    public void Paste() => Editor.Paste();

    private void OnTextChanged(object? sender, EventArgs e)
    {
        UpdateMiniMap();
        if (!suppressChanges) ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyLanguage(string? extension) => highlightingService.Apply(Editor, extension);

    private void UpdateMiniMap()
    {
        MiniMapText.Text = string.Join(Environment.NewLine,
            Editor.Text.Split(["\r\n", "\n"], StringSplitOptions.None)
                .Select(line => line.Length > 160 ? line[..160] : line));
    }

    private void OnMiniMapMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Editor.Document.LineCount <= 1 || MiniMapScrollViewer.ActualHeight <= 0) return;
        var point = e.GetPosition(MiniMapScrollViewer);
        var ratio = Math.Clamp(point.Y / MiniMapScrollViewer.ActualHeight, 0, 1);
        var line = Math.Max(1, (int)Math.Round(ratio * (Editor.Document.LineCount - 1)) + 1);
        Editor.ScrollToLine(line);
        Editor.Focus();
    }

    private void OnTextEntering(object? sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text)) return;
        var caretOffset = Editor.CaretOffset;
        if (caretOffset < Editor.Text.Length && IsClosingCharacter(e.Text[0]) && Editor.Text[caretOffset] == e.Text[0])
        {
            Editor.CaretOffset++;
            e.Handled = true;
            return;
        }

        if (GetClosingCharacter(e.Text[0]) is { } closing)
        {
            Editor.Document.Insert(caretOffset, closing.ToString());
            Editor.CaretOffset = caretOffset;
        }
    }

    private void OnTextEntered(object? sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        if (e.Text != "\n") return;
        var caretOffset = Editor.CaretOffset;
        if (caretOffset == 0) return;
        var previousLine = Editor.Document.GetLineByOffset(caretOffset - 1);
        var previousText = Editor.Document.GetText(previousLine.Offset, previousLine.Length);
        var indentation = new string(previousText.TakeWhile(c => c is ' ' or '\t').ToArray());
        if (previousText.TrimEnd().EndsWith("{", StringComparison.Ordinal)) indentation += new string(' ', IndentSize);
        if (GetCurrentLineStartsWithClosingBrace(caretOffset))
            indentation = indentation.Length >= IndentSize ? indentation[..^IndentSize] : string.Empty;
        if (indentation.Length == 0) return;
        Editor.Document.Insert(caretOffset, indentation);
        Editor.CaretOffset = caretOffset + indentation.Length;
    }

    private bool GetCurrentLineStartsWithClosingBrace(int offset)
    {
        if (offset >= Editor.Document.TextLength) return false;
        var line = Editor.Document.GetLineByOffset(offset);
        return Editor.Document.GetText(offset, line.EndOffset - offset).TrimStart().StartsWith("}", StringComparison.Ordinal);
    }

    private static bool IsClosingCharacter(char character) => character is ')' or ']' or '}' or '"' or '\'';

    private static char? GetClosingCharacter(char character) => character switch
    {
        '(' => ')',
        '[' => ']',
        '{' => '}',
        '"' => '"',
        '\'' => '\'',
        _ => null
    };
}
