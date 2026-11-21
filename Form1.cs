using System.Drawing;
using System.Windows.Forms.Integration;
using KeyceCode.Models;
using KeyceCode.Services;
using KeyceCode.ViewModels;
using KeyceCode.Views;

namespace KeyceCode;

public sealed class Form1 : Form
{
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill, Appearance = TabAppearance.Normal };
    private readonly ExplorerView explorer = new(new ProjectViewModel(new FileService(), new ProjectService()));
    private readonly FileService fileService = new();
    private readonly CodeAnalysisService codeAnalysisService = new();
    private readonly List<(int Start, int End, int Line, int Column)> diagnosticRanges = new();
    private readonly RichTextBox output = new() { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.FromArgb(20, 22, 27), ForeColor = Color.Gainsboro, BorderStyle = BorderStyle.None, Font = new Font("Cascadia Mono", 10) };
    private readonly ToolStripStatusLabel status = new("Prêt") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

    // MEMBRE 4 : compilation, exécution, console et terminal intégrés.
    private readonly OutputViewModel outputViewModel = new();
    private readonly TerminalViewModel terminalViewModel = new();
    private readonly TabControl bottomTabs = new() { Dock = DockStyle.Fill, Appearance = TabAppearance.Normal };
    private readonly TabPage consoleTab = new("Console") { BackColor = Color.FromArgb(30, 30, 30) };
    private readonly TabPage terminalTab = new("Terminal") { BackColor = Color.FromArgb(30, 30, 30) };
    private readonly TabPage analysisTab = new("Analyse") { BackColor = Color.FromArgb(20, 22, 27) };

    private string? workspace;
    private string? buildTarget;

    public Form1()
    {
        Text = "KeyceCode"; Width = 1280; Height = 800; MinimumSize = new Size(900, 600);
        BackColor = Color.FromArgb(37, 40, 48); ForeColor = Color.Gainsboro; StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
        output.MouseDoubleClick += Output_MouseDoubleClick;
        ConnectRunTools();
        FormClosing += (_, e) =>
        {
            if (tabs.TabPages.Cast<TabPage>().Any(t => !CanClose(t))) { e.Cancel = true; return; }
            StopAll();
        };
    }

    private void BuildLayout()
    {
        var menu = new MenuStrip { BackColor = Color.FromArgb(30, 33, 40), ForeColor = Color.Gainsboro, Renderer = new ToolStripProfessionalRenderer(new DarkColors()) };
        var fileMenu = new ToolStripMenuItem("Fichier");
        Add(fileMenu, "Nouveau", (_, _) => NewFile(), Keys.Control | Keys.N);
        Add(fileMenu, "Ouvrir…", (_, _) => OpenFile(), Keys.Control | Keys.O);
        Add(fileMenu, "Ouvrir un projet C#…", (_, _) => OpenProject());
        Add(fileMenu, "Ouvrir un dossier de projet…", (_, _) => OpenWorkspace());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        Add(fileMenu, "Enregistrer", (_, _) => SaveCurrent(), Keys.Control | Keys.S);
        Add(fileMenu, "Enregistrer sous…", (_, _) => SaveAs());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        Add(fileMenu, "Fermer l’onglet", (_, _) => CloseCurrent());
        Add(fileMenu, "Quitter", (_, _) => Close());
        var editMenu = new ToolStripMenuItem("Édition");
        Add(editMenu, "Annuler", (_, _) => Editor()?.Undo(), Keys.Control | Keys.Z);
        Add(editMenu, "Rétablir", (_, _) => Editor()?.Redo(), Keys.Control | Keys.Y);
        Add(editMenu, "Couper", (_, _) => Editor()?.Cut(), Keys.Control | Keys.X);
        Add(editMenu, "Copier", (_, _) => Editor()?.Copy(), Keys.Control | Keys.C);
        Add(editMenu, "Coller", (_, _) => Editor()?.Paste(), Keys.Control | Keys.V);
        Add(editMenu, "Rechercher…", (_, _) => FindText(), Keys.Control | Keys.F);
        var runMenu = new ToolStripMenuItem("Exécuter");
        Add(runMenu, "Compiler", (_, _) => CompileProject(), Keys.Control | Keys.Shift | Keys.B);
        Add(runMenu, "Lancer le fichier", async (_, _) => await RunCurrent(), Keys.F5);
        Add(runMenu, "Arrêter", (_, _) => StopAll(), Keys.Shift | Keys.F5);
        runMenu.DropDownItems.Add(new ToolStripSeparator());
        Add(runMenu, "Analyser le code", (_, _) => AnalyzeCurrent());
        menu.Items.AddRange([fileMenu, editMenu, runMenu]); MainMenuStrip = menu;

        var bar = new ToolStrip { BackColor = Color.FromArgb(30, 33, 40), ForeColor = Color.Gainsboro, GripStyle = ToolStripGripStyle.Hidden, Renderer = new ToolStripProfessionalRenderer(new DarkColors()) };
        Button(bar, "＋ Nouveau", () => NewFile()); Button(bar, "Ouvrir", OpenFile); Button(bar, "Projet C#", OpenProject); Button(bar, "Dossier", OpenWorkspace); Button(bar, "Enregistrer", SaveCurrent);
        bar.Items.Add(new ToolStripSeparator());
        Button(bar, "🔨 Compiler", CompileProject);
        Button(bar, "▶ Exécuter (F5)", async () => await RunCurrent());
        Button(bar, "■ Arrêter", StopAll);
        Button(bar, "🔍 Analyser", AnalyzeCurrent);

        var split = new SplitContainer { Size = new Size(1200, 700), Dock = DockStyle.Fill, SplitterDistance = 260, BackColor = Color.FromArgb(37, 40, 48), Panel1MinSize = 180, Panel2MinSize = 400 };
        split.Panel1.Controls.Add(explorer);
        explorer.FileOpenRequested += OpenPath;
        explorer.ProjectOpened += project =>
        {
            workspace = project.RootPath;
            buildTarget = project.EntryFilePath is { } entry && entry.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ? entry : project.RootPath;
            terminalViewModel.SetWorkingDirectory(project.RootPath);
            UpdateBuildTarget();
            status.Text = $"Projet : {project.Name}";
        };
        explorer.ItemRenamed += UpdateOpenPaths;
        explorer.BeforeDelete += ConfirmOpenFilesBeforeDelete;
        var editorOutput = new SplitContainer { Size = new Size(900, 700), Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 520, BackColor = Color.FromArgb(37, 40, 48), Panel1MinSize = 180, Panel2MinSize = 100 };
        editorOutput.Panel1.Controls.Add(tabs);
        BuildBottomPanel();
        editorOutput.Panel2.Controls.Add(bottomTabs); split.Panel2.Controls.Add(editorOutput);
        var strip = new StatusStrip { BackColor = Color.FromArgb(0, 104, 156), ForeColor = Color.White }; strip.Items.Add(status);
        Controls.Add(split); Controls.Add(bar); Controls.Add(menu); Controls.Add(strip);
        bar.Dock = DockStyle.Top; menu.Dock = DockStyle.Top; strip.Dock = DockStyle.Bottom;
        tabs.SelectedIndexChanged += (_, _) => UpdateBuildTarget();
        NewFile();
    }

    private void BuildBottomPanel()
    {
        consoleTab.Controls.Add(new ElementHost { Dock = DockStyle.Fill, Child = new OutputView { DataContext = outputViewModel } });
        terminalTab.Controls.Add(new ElementHost { Dock = DockStyle.Fill, Child = new TerminalView { DataContext = terminalViewModel } });
        analysisTab.Controls.Add(output);
        bottomTabs.TabPages.AddRange(new[] { consoleTab, terminalTab, analysisTab });
    }

    private static void Add(ToolStripMenuItem parent, string label, EventHandler action, Keys shortcut = Keys.None)
    { var item = new ToolStripMenuItem(label) { ShortcutKeys = shortcut }; item.Click += action; parent.DropDownItems.Add(item); }
    private static void Button(ToolStrip bar, string label, Action action)
    { var item = new ToolStripButton(label); item.Click += (_, _) => action(); bar.Items.Add(item); }
    private CodeEditorSurface? Editor() => EditorOf(tabs.SelectedTab);
    private static CodeEditorSurface? EditorOf(TabPage? page) => page?.Controls.OfType<CodeEditorSurface>().FirstOrDefault();
    private string? PathOf(TabPage? page = null) => (page ?? tabs.SelectedTab)?.Tag as string;

    private void NewFile()
    {
        var page = new TabPage("Sans titre") { Tag = null, BackColor = Color.FromArgb(30, 33, 40) };
        var surface = new CodeEditorSurface(); surface.Modified = false;
        page.Controls.Add(surface); tabs.TabPages.Add(page); tabs.SelectedTab = page; UpdateTitle(page);
    }
    private void OpenFile()
    {
        using var dialog = new OpenFileDialog { Filter = "Tous les fichiers (*.*)|*.*|Code source|*.cs;*.py;*.js;*.html;*.css;*.json;*.txt", FilterIndex = 1 };
        if (dialog.ShowDialog(this) == DialogResult.OK) OpenPath(dialog.FileName);
    }
    private void OpenPath(string path)
    {
        var existing = tabs.TabPages.Cast<TabPage>().FirstOrDefault(t => string.Equals(PathOf(t), path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) { tabs.SelectedTab = existing; return; }
        try
        {
            var page = new TabPage(Path.GetFileName(path)) { Tag = path, BackColor = Color.FromArgb(30, 33, 40) };
            var surface = new CodeEditorSurface(); page.Controls.Add(surface); surface.ApplyLanguage(Path.GetExtension(path)); surface.Text = fileService.ReadText(path); surface.Modified = false;
            tabs.TabPages.Add(page); tabs.SelectedTab = page; UpdateTitle(page); status.Text = path;
        }
        catch (Exception ex) { MessageBox.Show(this, $"Impossible d’ouvrir le fichier.\n{ex.Message}", "KeyceCode", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    private void OpenProject() => explorer.OpenProjectDialog(this);

    private void OpenWorkspace()
    {
        using var dialog = new FolderBrowserDialog { Description = "Choisissez le dossier contenant un projet C#" };
        if (dialog.ShowDialog(this) == DialogResult.OK) explorer.OpenProject(dialog.SelectedPath);
    }

    private void SaveCurrent() { if (tabs.SelectedTab is { } page) SavePage(page); }
    private void SaveAs()
    {
        if (tabs.SelectedTab is not { } page) return;
        using var dialog = new SaveFileDialog { FileName = PathOf(page) is { } p ? Path.GetFileName(p) : "Sans titre.cs", Filter = "Tous les fichiers (*.*)|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK) SavePage(page, dialog.FileName);
    }
    private void SavePage(TabPage page, string? requestedPath = null)
    {
        if (EditorOf(page) is not { } editor) return;
        var path = requestedPath ?? PathOf(page);
        if (path is null)
        {
            using var dialog = new SaveFileDialog { FileName = "NouveauFichier.cs", Filter = "Tous les fichiers (*.*)|*.*" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            path = dialog.FileName;
        }
        try { fileService.WriteText(path, editor.Text); page.Tag = Path.GetFullPath(path); editor.Modified = false; UpdateTitle(page); UpdateBuildTarget(); status.Text = $"Enregistré : {path}"; if (workspace is not null) explorer.RefreshProject(); }
        catch (Exception ex) { MessageBox.Show(this, $"Impossible d’enregistrer.\n{ex.Message}", "KeyceCode", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    private void UpdateTitle(TabPage page)
    {
        var editor = EditorOf(page);
        page.Text = (PathOf(page) is { } p ? Path.GetFileName(p) : "Sans titre") + (editor?.Modified == true ? " •" : "");
        Text = $"{page.Text} — KeyceCode";
        if (editor is not null) { editor.TextChanged -= OnEditorChanged; editor.TextChanged += OnEditorChanged; }
    }
    private void OnEditorChanged(object? sender, EventArgs e)
    {
        if (sender is not CodeEditorSurface editor) return;
        var page = tabs.TabPages.Cast<TabPage>().FirstOrDefault(t => EditorOf(t) == editor); if (page is null) return;
        UpdateTitle(page);
    }

    private void UpdateOpenPaths(string oldPath, string newPath)
    {
        var directoryPrefix = oldPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var page in tabs.TabPages.Cast<TabPage>())
        {
            if (PathOf(page) is not { } path) continue;
            if (string.Equals(path, oldPath, StringComparison.OrdinalIgnoreCase)) page.Tag = newPath;
            else if (path.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase)) page.Tag = newPath + path[(directoryPrefix.Length - 1)..];
            else continue;
            UpdateTitle(page);
        }
        UpdateBuildTarget();
    }

    private bool ConfirmOpenFilesBeforeDelete(string targetPath)
    {
        var fullTarget = Path.GetFullPath(targetPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var prefix = fullTarget + Path.DirectorySeparatorChar;
        var openPages = tabs.TabPages.Cast<TabPage>()
            .Where(page => PathOf(page) is { } path &&
                (string.Equals(Path.GetFullPath(path), fullTarget, StringComparison.OrdinalIgnoreCase) ||
                 Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        foreach (var page in openPages)
            if (!CanClose(page)) return false;
        foreach (var page in openPages) { tabs.TabPages.Remove(page); page.Dispose(); }
        if (tabs.TabCount == 0) NewFile();
        return true;
    }
    private bool CanClose(TabPage page)
    {
        if (EditorOf(page)?.Modified == true)
        {
            var answer = MessageBox.Show(this, $"Enregistrer les modifications de « {page.Text.TrimEnd(' ', '•')} » ?", "KeyceCode", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (answer == DialogResult.Cancel) return false; if (answer == DialogResult.Yes) { SavePage(page); if (EditorOf(page)?.Modified == true) return false; }
        }
        return true;
    }
    private void CloseCurrent()
    { if (tabs.SelectedTab is not { } page || !CanClose(page)) return; tabs.TabPages.Remove(page); page.Dispose(); if (tabs.TabCount == 0) NewFile(); }

    private void FindText()
    {
        var editor = Editor(); if (editor is null) return;
        var term = Microsoft.VisualBasic.Interaction.InputBox("Texte à rechercher :", "Rechercher");
        if (term.Length == 0) return;
        var index = editor.Text.IndexOf(term, editor.SelectionStart + editor.SelectionLength, StringComparison.CurrentCultureIgnoreCase);
        if (index < 0) index = editor.Text.IndexOf(term, StringComparison.CurrentCultureIgnoreCase);
        if (index >= 0) { editor.Select(index, term.Length); editor.Focus(); } else status.Text = "Aucun résultat";
    }

    private void AnalyzeCurrent()
    {
        var editor = Editor();
        if (editor is null) return;
        ShowBottomTab(analysisTab);

        try
        {
            var diagnostics = codeAnalysisService.Analyze(editor.Text);
            output.Clear();
            diagnosticRanges.Clear();

            if (diagnostics.Count == 0)
            {
                output.Text = "Aucune erreur ni aucun avertissement détecté.";
                status.Text = "Analyse terminée.";
                return;
            }

            foreach (var diagnostic in diagnostics)
            {
                var start = output.TextLength;
                output.AppendText($"{diagnostic.Severity} | {diagnostic.Code} | Ligne {diagnostic.Line}, Colonne {diagnostic.Column}\r\n{diagnostic.Message}\r\n\r\n");
                diagnosticRanges.Add((start, output.TextLength, diagnostic.Line, diagnostic.Column));
            }

            status.Text = $"Analyse terminée : {diagnostics.Count} problème(s).";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erreur d'analyse", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Output_MouseDoubleClick(object? sender, MouseEventArgs e)
    {
        var index = output.GetCharIndexFromPosition(e.Location);
        var diagnostic = diagnosticRanges.FirstOrDefault(d => index >= d.Start && index < d.End);
        if (diagnostic.Line == 0) return;

        var editor = Editor();
        if (editor is null) return;

        var position = GetLineStartOffset(editor.Text, diagnostic.Line) + Math.Max(0, diagnostic.Column - 1);
        editor.Select(Math.Min(position, editor.Text.Length), 0);
        editor.Focus();
        status.Text = $"Erreur sélectionnée : ligne {diagnostic.Line}, colonne {diagnostic.Column}";
    }

    private static int GetLineStartOffset(string text, int lineNumber)
    {
        if (lineNumber <= 1) return 0;
        var currentLine = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;
            currentLine++;
            if (currentLine == lineNumber) return i + 1;
        }
        return text.Length;
    }

    // ------------------------------------------------------------------
    // Compilation / exécution (MEMBRE 4)
    // ------------------------------------------------------------------

    private void ConnectRunTools()
    {
        // Enregistre les fichiers modifiés avant chaque compilation ou exécution.
        outputViewModel.SaveAllRequested += SaveAllOpenFiles;
        // Double-clic sur une erreur de compilation : ouvre le fichier et va à la ligne.
        outputViewModel.DiagnosticActivated += OpenDiagnostic;
        // Le message d'état de la console est répété dans la barre d'état de la fenêtre.
        outputViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OutputViewModel.Status)) status.Text = outputViewModel.Status;
        };
        UpdateBuildTarget();
    }

    private void ShowBottomTab(TabPage page) => bottomTabs.SelectedTab = page;

    /// <summary>Projet ouvert, sinon fichier .cs courant : c'est ce que « Compiler » et « Exécuter » utilisent.</summary>
    private void UpdateBuildTarget()
    {
        try
        {
            outputViewModel.ProjectPath = ResolveBuildTarget();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            outputViewModel.ProjectPath = null;
        }
    }

    private string? ResolveBuildTarget()
    {
        if (buildTarget is not null) return buildTarget;
        var file = PathOf();
        if (file is null || !file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return null;
        // Un .cs ouvert à côté d'un .csproj est compilé avec son projet, sinon en fichier seul.
        var directory = Path.GetDirectoryName(file);
        return directory is not null && Directory.EnumerateFiles(directory, "*.csproj").Any() ? directory : file;
    }

    private void SaveAllOpenFiles()
    {
        foreach (var page in tabs.TabPages.Cast<TabPage>().ToList())
            if (EditorOf(page)?.Modified == true && PathOf(page) is not null) SavePage(page);
    }

    private void OpenDiagnostic(BuildDiagnostic diagnostic)
    {
        if (!File.Exists(diagnostic.File)) { status.Text = $"Fichier introuvable : {diagnostic.File}"; return; }
        OpenPath(Path.GetFullPath(diagnostic.File));
        if (Editor() is not { } editor) return;
        var position = GetLineStartOffset(editor.Text, diagnostic.Line) + Math.Max(0, diagnostic.Column - 1);
        editor.Select(Math.Min(position, editor.Text.Length), 0);
        editor.Focus();
        status.Text = $"{diagnostic.Code} : ligne {diagnostic.Line}, colonne {diagnostic.Column}";
    }

    private void CompileProject()
    {
        ShowBottomTab(consoleTab);
        if (!outputViewModel.CompileCommand.CanExecute(null)) { status.Text = "Une opération est déjà en cours."; return; }
        outputViewModel.CompileCommand.Execute(null);
    }

    private void RunProject()
    {
        ShowBottomTab(consoleTab);
        if (!outputViewModel.RunCommand.CanExecute(null)) { status.Text = "Une opération est déjà en cours."; return; }
        outputViewModel.RunCommand.Execute(null);
    }

    private void StopAll()
    {
        outputViewModel.Stop();
        terminalViewModel.Stop();
    }

    private async Task RunCurrent()
    {
        var editor = Editor(); if (editor is null) return;
        if (PathOf() is null) { SaveCurrent(); if (PathOf() is null) return; }
        SaveCurrent(); var file = PathOf()!; var ext = Path.GetExtension(file).ToLowerInvariant();

        // C# : compilation et exécution via le SDK .NET (projet ouvert ou fichier seul).
        if (ext == ".cs") { RunProject(); return; }

        // Autres langages : lancés dans la même console (sortie en direct, saisie clavier, bouton Arrêter).
        var (exe, args) = ext switch
        {
            ".py" => ("python", Quote(file)), ".js" => ("node", Quote(file)), ".ps1" => ("powershell", $"-NoProfile -ExecutionPolicy Bypass -File {Quote(file)}"),
            ".bat" or ".cmd" => ("cmd", $"/c {Quote(file)}"),
            _ => ("", "")
        };
        ShowBottomTab(consoleTab);
        if (exe.Length == 0)
        {
            outputViewModel.Log.Clear();
            outputViewModel.Log.AppendLine($"Aucun exécuteur configuré pour {ext}.", OutputKind.Error);
            outputViewModel.Log.AppendLine("Langages pris en charge : C#, Python, JavaScript, PowerShell et Batch.", OutputKind.Info);
            return;
        }
        if (outputViewModel.IsBusy) { status.Text = "Une opération est déjà en cours."; return; }
        await outputViewModel.RunToolAsync(exe, args, Path.GetDirectoryName(file)!, $"{exe} {args}");
    }
    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F5) { _ = RunCurrent(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    private sealed class DarkColors : ProfessionalColorTable
    {
        public override Color MenuBorder => Color.FromArgb(60, 64, 72); public override Color MenuItemSelected => Color.FromArgb(55, 60, 70);
        public override Color MenuItemBorder => Color.FromArgb(70, 75, 85); public override Color ToolStripDropDownBackground => Color.FromArgb(30, 33, 40);
        public override Color ImageMarginGradientBegin => Color.FromArgb(30, 33, 40); public override Color ImageMarginGradientMiddle => Color.FromArgb(30, 33, 40);
        public override Color ImageMarginGradientEnd => Color.FromArgb(30, 33, 40); public override Color ToolStripGradientBegin => Color.FromArgb(30, 33, 40);
        public override Color ToolStripGradientMiddle => Color.FromArgb(30, 33, 40); public override Color ToolStripGradientEnd => Color.FromArgb(30, 33, 40);
    }
}
