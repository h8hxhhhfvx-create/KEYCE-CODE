using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit;
using KeyceCode.Models;
using KeyceCode.Services;
using WpfUserControl = System.Windows.Controls.UserControl;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace KeyceCode.Views;

public partial class EditorView : WpfUserControl
{
    private readonly SyntaxHighlightingService highlightingService = new();
    private readonly AutoCompleteService autoCompleteService = new();
    private CompletionWindow? completionWindow;
    private string savedText = string.Empty;
    private int indentSize = 4;
    private bool suppressChanges;
    private bool examMode;
    private readonly DiagnosticUnderlineRenderer diagnosticRenderer = new();
    private readonly DiagnosticMargin diagnosticMargin = new();
    private readonly BraceFoldingStrategy foldingStrategy = new();
    private FoldingManager? foldingManager;
    private IReadOnlyList<DiagnosticMarker> diagnosticMarkers = [];
    private string commentPrefix = "//";
    private TextViewPosition? rectangleAnchor;

    public EditorView()
    {
        InitializeComponent();
        foldingManager = FoldingManager.Install(Editor.TextArea);
        highlightingService.Apply(Editor);
        Editor.TextArea.TextView.BackgroundRenderers.Add(diagnosticRenderer);
        Editor.TextArea.LeftMargins.Insert(0, diagnosticMargin);
        Editor.TextArea.TextView.ScrollOffsetChanged += (_, _) => diagnosticMargin.InvalidateVisual();
        Editor.TextArea.TextEntering += OnTextEntering;
        Editor.TextArea.TextEntered += OnTextEntered;
        Editor.TextArea.KeyDown += OnEditorKeyDown;
        Editor.TextArea.TextView.MouseMove += OnDiagnosticMouseMove;
        Editor.TextArea.TextView.MouseLeave += OnDiagnosticMouseLeave;
        Editor.AddHandler(System.Windows.Input.CommandManager.PreviewExecutedEvent,
            new System.Windows.Input.ExecutedRoutedEventHandler(OnPreviewExecuted));
        Editor.TextArea.Caret.PositionChanged += (_, _) => CaretPositionChanged?.Invoke(this, EventArgs.Empty);
        Editor.TextChanged += OnTextChanged;
        UpdateMiniMap();
    }

    public event EventHandler? ContentChanged;
    public event EventHandler? CaretPositionChanged;

    public (int Line, int Column) CaretPosition
    {
        get
        {
            var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
            return (line.LineNumber, Editor.CaretOffset - line.Offset + 1);
        }
    }

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

    public bool IsReadOnly
    {
        get => Editor.IsReadOnly;
        set => Editor.IsReadOnly = value;
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

    public void ReplaceText(string text)
    {
        Editor.Document.Replace(0, Editor.Document.TextLength, text ?? string.Empty);
        Editor.Focus();
    }

    public void ToggleLineComment()
    {
        if (Editor.Document.LineCount == 0) return;
        var start = Editor.SelectionStart;
        var end = Math.Max(start, start + Editor.SelectionLength);
        var first = Editor.Document.GetLineByOffset(Math.Clamp(start, 0, Editor.Document.TextLength));
        var last = Editor.Document.GetLineByOffset(Math.Clamp(end, 0, Editor.Document.TextLength));
        var lines = Enumerable.Range(first.LineNumber, last.LineNumber - first.LineNumber + 1)
            .Select(Editor.Document.GetLineByNumber)
            .ToArray();
        var nonEmpty = lines.Where(line => line.Length > 0).ToArray();
        var uncomment = nonEmpty.Length > 0 && nonEmpty.All(line =>
            Editor.Document.GetText(line.Offset, line.Length).TrimStart().StartsWith(commentPrefix, StringComparison.Ordinal));

        foreach (var line in lines.Reverse())
        {
            var text = Editor.Document.GetText(line.Offset, line.Length);
            if (text.Trim().Length == 0) continue;
            var indentation = text.Length - text.TrimStart().Length;
            var contentOffset = line.Offset + indentation;
            if (uncomment)
            {
                var removeLength = commentPrefix.Length;
                if (contentOffset + removeLength < line.EndOffset && Editor.Document.GetCharAt(contentOffset + removeLength) == ' ')
                    removeLength++;
                Editor.Document.Remove(contentOffset, Math.Min(removeLength, line.EndOffset - contentOffset));
            }
            else
            {
                Editor.Document.Insert(contentOffset, commentPrefix + " ");
            }
        }
        UpdateFolding();
        Editor.Focus();
    }

    public void SetDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        var markers = new List<DiagnosticMarker>();
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Line <= 0 || diagnostic.Line > Editor.Document.LineCount) continue;

            var startLine = Editor.Document.GetLineByNumber(diagnostic.Line);
            var start = Math.Clamp(startLine.Offset + Math.Max(0, diagnostic.Column - 1), startLine.Offset, startLine.EndOffset);
            var end = start + 1;
            if (diagnostic.EndLine >= diagnostic.Line && diagnostic.EndLine <= Editor.Document.LineCount)
            {
                var endLine = Editor.Document.GetLineByNumber(diagnostic.EndLine);
                end = Math.Clamp(endLine.Offset + Math.Max(0, diagnostic.EndColumn - 1), start, endLine.EndOffset);
            }

            if (end <= start) end = Math.Min(Editor.Document.TextLength, start + 1);
            if (end > start)
                markers.Add(new DiagnosticMarker(
                    start,
                    end - start,
                    diagnostic.Severity,
                    diagnostic.Line,
                    diagnostic.Code,
                    diagnostic.Message));
        }

        diagnosticMarkers = markers;
        diagnosticRenderer.SetMarkers(markers);
        diagnosticMargin.SetMarkers(markers);
        ClearDiagnosticToolTip();
        Editor.TextArea.TextView.Redraw();
        diagnosticMargin.InvalidateVisual();
    }

    private void OnDiagnosticMouseMove(object? sender, WpfMouseEventArgs e)
    {
        var position = Editor.GetPositionFromPoint(e.GetPosition(Editor));
        if (position is null) return;

        var offset = Editor.Document.GetOffset(position.Value.Location);
        var marker = diagnosticMarkers.FirstOrDefault(item =>
            offset >= item.StartOffset && offset <= item.StartOffset + item.Length);
        if (marker.Length == 0) return;

        var severity = string.Equals(marker.Severity, "error", StringComparison.OrdinalIgnoreCase)
            ? "Erreur"
            : "Avertissement";
        var panel = new StackPanel { MaxWidth = 520 };
        panel.Children.Add(new TextBlock
        {
            Text = $"{severity} {marker.Code}",
            FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White)
        });
        panel.Children.Add(new TextBlock
        {
            Text = marker.Message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White)
        });
        var tooltip = new System.Windows.Controls.ToolTip { Content = panel, Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 48)) };
        System.Windows.Controls.ToolTipService.SetShowDuration(tooltip, 30000);
        Editor.ToolTip = tooltip;
        tooltip.IsOpen = true;
    }

    private void OnDiagnosticMouseLeave(object? sender, WpfMouseEventArgs e) => ClearDiagnosticToolTip();

    private void ClearDiagnosticToolTip()
    {
        if (Editor.ToolTip is System.Windows.Controls.ToolTip tooltip) tooltip.IsOpen = false;
        Editor.ToolTip = null;
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        UpdateMiniMap();
        UpdateFolding();
        if (!suppressChanges) ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyLanguage(string? extension)
    {
        var normalized = extension?.Trim().ToLowerInvariant();
        commentPrefix = normalized switch
        {
            ".py" or ".ps1" or ".rb" or ".sh" => "#",
            ".html" or ".htm" or ".xml" or ".xaml" => "<!--",
            _ => "//"
        };
        highlightingService.Apply(Editor, extension);
        UpdateFolding();
    }

    private void UpdateFolding()
    {
        if (foldingManager is null) return;
        foldingManager.UpdateFoldings(foldingStrategy.CreateNewFoldings(Editor.Document, out _), -1);
    }

    public void ApplyAppearance(AppSettings settings)
    {
        var dark = !string.Equals(settings.Theme, "Light", StringComparison.OrdinalIgnoreCase);
        var background = dark ? "#1E2128" : "#FAFAFA";
        var foreground = dark ? "#DCDCDC" : "#202020";
        var minimapBackground = dark ? "#181A20" : "#F0F2F5";
        var minimapForeground = dark ? "#737B8C" : "#7A8492";
        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(background));
        Editor.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(background));
        Editor.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(foreground));
        MiniMapBorder.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(minimapBackground));
        MiniMapScrollViewer.Background = MiniMapBorder.Background;
        MiniMapText.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(minimapForeground));
        Editor.FontSize = Math.Clamp(settings.EditorFontSize, 8, 32);
        indentSize = Math.Clamp(settings.TabSize, 1, 16);
        Editor.Options.IndentationSize = indentSize;
        Editor.Options.ConvertTabsToSpaces = true;
        Editor.WordWrap = settings.WordWrap;
    }

    public void SetExamMode(bool enabled) => examMode = enabled;

    private void OnPreviewExecuted(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        if (!examMode) return;
        if (e.Command == System.Windows.Input.ApplicationCommands.Copy ||
            e.Command == System.Windows.Input.ApplicationCommands.Cut ||
            e.Command == System.Windows.Input.ApplicationCommands.Paste)
            e.Handled = true;
    }

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
        if (e.Text is not "\n" && (char.IsLetterOrDigit(e.Text.FirstOrDefault()) || e.Text == "."))
        {
            ShowCompletion();
            return;
        }
        if (e.Text != "\n") return;
        var caretOffset = Editor.CaretOffset;
        if (caretOffset == 0) return;
        var previousLine = Editor.Document.GetLineByOffset(caretOffset - 1);
        var previousText = Editor.Document.GetText(previousLine.Offset, previousLine.Length);
        var indentation = new string(previousText.TakeWhile(c => c is ' ' or '\t').ToArray());
        if (previousText.TrimEnd().EndsWith("{", StringComparison.Ordinal)) indentation += new string(' ', indentSize);
        if (GetCurrentLineStartsWithClosingBrace(caretOffset))
            indentation = indentation.Length >= indentSize ? indentation[..^indentSize] : string.Empty;
        if (indentation.Length == 0) return;
        Editor.Document.Insert(caretOffset, indentation);
        Editor.CaretOffset = caretOffset + indentation.Length;
    }

    private void ShowCompletion()
    {
        completionWindow?.Close();
        var suggestions = autoCompleteService.GetCompletionItems(Editor.Text, Editor.CaretOffset);
        if (suggestions.Count == 0) return;
        completionWindow = new CompletionWindow(Editor.TextArea);
        foreach (var suggestion in suggestions)
            completionWindow.CompletionList.CompletionData.Add(new EditorCompletionData(suggestion));
        completionWindow.Closed += (_, _) => completionWindow = null;
        completionWindow.Show();
    }

    private void OnEditorKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var modifiers = System.Windows.Input.Keyboard.Modifiers;
        if (modifiers == (System.Windows.Input.ModifierKeys.Alt | System.Windows.Input.ModifierKeys.Shift) &&
            e.Key is System.Windows.Input.Key.Down or System.Windows.Input.Key.Up)
        {
            ExtendRectangleSelection(e.Key == System.Windows.Input.Key.Down ? 1 : -1);
            e.Handled = true;
            return;
        }

        if (e.Key == System.Windows.Input.Key.Space &&
            modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
        {
            ShowCompletion();
            e.Handled = true;
        }
    }

    private void ExtendRectangleSelection(int direction)
    {
        var current = new TextViewPosition(Editor.Document.GetLocation(Editor.CaretOffset));
        rectangleAnchor ??= current;
        var targetLine = Math.Clamp(current.Line + direction, 1, Editor.Document.LineCount);
        var targetLocation = new TextLocation(targetLine, Math.Max(1, current.Column));
        var target = new TextViewPosition(targetLocation);
        Editor.CaretOffset = Editor.Document.GetOffset(targetLocation);
        Editor.TextArea.Selection = new RectangleSelection(Editor.TextArea, rectangleAnchor.Value, target);
        Editor.Focus();
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

    private sealed class EditorCompletionData(CompletionSuggestion suggestion) : ICompletionData
    {
        public System.Windows.Media.ImageSource? Image => null;
        public string Text { get; } = suggestion.DisplayText;
        public object Content => Text;
        public object Description => suggestion.Kind;
        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, suggestion.InsertText);
        }
    }

    private sealed class BraceFoldingStrategy
    {
        public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
        {
            firstErrorOffset = -1;
            var stack = new Stack<int>();
            var foldings = new List<NewFolding>();
            for (var offset = 0; offset < document.TextLength; offset++)
            {
                var character = document.GetCharAt(offset);
                if (character == '{') stack.Push(offset);
                else if (character == '}' && stack.Count > 0)
                {
                    var start = stack.Pop();
                    var startLine = document.GetLineByOffset(start);
                    var endLine = document.GetLineByOffset(offset);
                    if (startLine.LineNumber < endLine.LineNumber)
                        foldings.Add(new NewFolding(start, offset + 1) { Name = "…" });
                }
            }
            return foldings.OrderBy(folding => folding.StartOffset);
        }
    }

    private sealed class DiagnosticUnderlineRenderer : IBackgroundRenderer
    {
        private IReadOnlyList<DiagnosticMarker> markers = [];

        public KnownLayer Layer => KnownLayer.Selection;

        public void SetMarkers(IReadOnlyList<DiagnosticMarker> value) => markers = value;

        public void Draw(TextView textView, System.Windows.Media.DrawingContext drawingContext)
        {
            if (markers.Count == 0) return;

            foreach (var marker in markers)
            {
                var segment = new TextSegment { StartOffset = marker.StartOffset, Length = marker.Length };
                var color = marker.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase)
                    ? System.Windows.Media.Colors.Gold
                    : System.Windows.Media.Colors.Tomato;
                var pen = new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(color), 1.4);
                pen.Freeze();

                foreach (var rectangle in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                {
                    if (rectangle.Width <= 0) continue;
                    var geometry = new System.Windows.Media.StreamGeometry();
                    using (var context = geometry.Open())
                    {
                        var y = rectangle.Bottom - 1;
                        const double wave = 3;
                        context.BeginFigure(new System.Windows.Point(rectangle.Left, y), false, false);
                        for (var x = rectangle.Left; x < rectangle.Right; x += wave)
                            context.LineTo(new System.Windows.Point(Math.Min(x + wave, rectangle.Right), y + (((int)((x - rectangle.Left) / wave) % 2 == 0) ? 1.5 : -1.5)), true, false);
                    }
                    geometry.Freeze();
                    drawingContext.DrawGeometry(null, pen, geometry);
                }
            }
        }
    }

    private sealed class DiagnosticMargin : ICSharpCode.AvalonEdit.Editing.AbstractMargin
    {
        private IReadOnlyList<DiagnosticMarker> markers = [];

        public void SetMarkers(IReadOnlyList<DiagnosticMarker> value) => markers = value;

        protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize) =>
            new(16, availableSize.Height);

        protected override void OnRender(System.Windows.Media.DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (TextView is null || Document is null || !TextView.VisualLinesValid || markers.Count == 0) return;

            foreach (var marker in markers.GroupBy(item => item.Line).Select(group => group.First()))
            {
                var visualLine = TextView.GetVisualLine(marker.Line);
                if (visualLine is null || visualLine.TextLines.Count == 0) continue;

                var textLine = visualLine.TextLines[0];
                var top = visualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.TextTop) - TextView.ScrollOffset.Y;
                var bottom = visualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.TextBottom) - TextView.ScrollOffset.Y;
                if (bottom < 0 || top > ActualHeight) continue;

                var color = marker.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase)
                    ? System.Windows.Media.Colors.Gold
                    : System.Windows.Media.Colors.Tomato;
                var brush = new System.Windows.Media.SolidColorBrush(color);
                brush.Freeze();
                var geometry = new System.Windows.Media.StreamGeometry();
                using (var context = geometry.Open())
                {
                    var centerY = Math.Max(4, (top + bottom) / 2);
                    context.BeginFigure(new System.Windows.Point(2, centerY - 4), true, true);
                    context.LineTo(new System.Windows.Point(14, centerY), true, false);
                    context.LineTo(new System.Windows.Point(2, centerY + 4), true, false);
                }
                geometry.Freeze();
                drawingContext.DrawGeometry(brush, null, geometry);
            }
        }
    }

    private readonly record struct DiagnosticMarker(
        int StartOffset,
        int Length,
        string Severity,
        int Line,
        string Code,
        string Message);
}
