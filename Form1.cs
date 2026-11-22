using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms.Integration;
using KeyceCode.Helpers;
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
    private readonly MainViewModel viewModel = new();
    private readonly List<DiagnosticListItem> analysisDiagnostics = [];
    private readonly ListBox diagnosticsList = new() { Dock = DockStyle.Fill, IntegralHeight = false, DisplayMember = nameof(DiagnosticListItem.Display) };
    private SplitContainer? mainSplit;
    private SplitContainer? editorOutputSplit;
    private TabControl? outputTabs;
    private readonly ToolStripStatusLabel status = new("Prêt") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private string? workspace;

    public Form1()
    {
        Text = "KeyceCode"; Width = 1280; Height = 800; MinimumSize = new Size(900, 600);
        BackColor = Color.FromArgb(37, 40, 48); ForeColor = Color.Gainsboro; StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
        FormClosing += (_, e) =>
        {
            if (tabs.TabPages.Cast<TabPage>().Any(t => !CanClose(t))) { e.Cancel = true; return; }
            viewModel.Output.Stop();
            viewModel.Terminal.Stop();
        };
    }

    private void BuildLayout()
    {
        var menu = new MenuStrip { BackColor = Color.FromArgb(30, 33, 40), ForeColor = Color.Gainsboro, Renderer = new ToolStripProfessionalRenderer(new ThemeColors(true)) };
        var fileMenu = new ToolStripMenuItem("Fichier");
        Add(fileMenu, "Nouveau", (_, _) => NewFile(), KeyboardShortcutHelper.NewFile);
        Add(fileMenu, "Ouvrir…", (_, _) => OpenFile(), KeyboardShortcutHelper.OpenFile);
        Add(fileMenu, "Ouvrir un projet C#…", (_, _) => OpenProject());
        Add(fileMenu, "Ouvrir un dossier de projet…", (_, _) => OpenWorkspace());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        Add(fileMenu, "Enregistrer", (_, _) => SaveCurrent(), KeyboardShortcutHelper.SaveFile);
        Add(fileMenu, "Enregistrer sous…", (_, _) => SaveAs());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        Add(fileMenu, "Fermer l’onglet", (_, _) => CloseCurrent());
        Add(fileMenu, "Quitter", (_, _) => Close());
        var editMenu = new ToolStripMenuItem("Édition");
        Add(editMenu, "Annuler", (_, _) => Editor()?.Undo(), KeyboardShortcutHelper.Undo);
        Add(editMenu, "Rétablir", (_, _) => Editor()?.Redo(), KeyboardShortcutHelper.Redo);
        Add(editMenu, "Couper", (_, _) => Editor()?.Cut(), KeyboardShortcutHelper.Cut);
        Add(editMenu, "Copier", (_, _) => Editor()?.Copy(), KeyboardShortcutHelper.Copy);
        Add(editMenu, "Coller", (_, _) => Editor()?.Paste(), KeyboardShortcutHelper.Paste);
        Add(editMenu, "Rechercher…", (_, _) => FindText(), KeyboardShortcutHelper.Find);
        var runMenu = new ToolStripMenuItem("Exécuter"); Add(runMenu, "Lancer le fichier", async (_, _) => await RunCurrent(), KeyboardShortcutHelper.Run);
        var viewMenu = new ToolStripMenuItem("Affichage");
        Add(viewMenu, "Explorateur", (_, _) => ToggleExplorer(), KeyboardShortcutHelper.ToggleExplorer);
        Add(viewMenu, "Panneau inférieur", (_, _) => ToggleOutput(), KeyboardShortcutHelper.ToggleOutput);
        var themeMenu = new ToolStripMenuItem("Thème");
        themeMenu.DropDownItems.Add("Sombre", null, (_, _) => SetTheme(AppTheme.Dark));
        themeMenu.DropDownItems.Add("Clair", null, (_, _) => SetTheme(AppTheme.Light));
        viewMenu.DropDownItems.Add(themeMenu);
        Add(viewMenu, "Paramètres…", (_, _) => OpenSettings());
        var helpMenu = new ToolStripMenuItem("Aide");
        Add(helpMenu, "À propos de KeyceCode", (_, _) => MessageBox.Show(this, "KeyceCode — éditeur de code C# et multi-langages.", "À propos", MessageBoxButtons.OK, MessageBoxIcon.Information));
        menu.Items.AddRange([fileMenu, editMenu, viewMenu, runMenu, helpMenu]); MainMenuStrip = menu;

        var bar = new ToolStrip { BackColor = Color.FromArgb(30, 33, 40), ForeColor = Color.Gainsboro, GripStyle = ToolStripGripStyle.Hidden, Renderer = new ToolStripProfessionalRenderer(new ThemeColors(true)) };
        Button(bar, "＋ Nouveau", () => NewFile()); Button(bar, "↥ Ouvrir", OpenFile); Button(bar, "▣ Projet C#", OpenProject); Button(bar, "▤ Dossier", OpenWorkspace); Button(bar, "▣ Enregistrer", SaveCurrent);
        bar.Items.Add(new ToolStripSeparator()); Button(bar, "▶ Exécuter (F5)", async () => await RunCurrent());
        Button(bar, "🔍 Analyser", AnalyzeCurrent);

        var split = new SplitContainer { Size = new Size(1200, 700), Dock = DockStyle.Fill, SplitterDistance = 260, BackColor = Color.FromArgb(37, 40, 48), Panel1MinSize = 180, Panel2MinSize = 400 };
        mainSplit = split;
        split.Panel1.Controls.Add(explorer);
        explorer.FileOpenRequested += OpenPath;
        explorer.ProjectOpened += project =>
        {
            workspace = project.RootPath;
            viewModel.Output.ProjectPath = project.RootPath;
            viewModel.Terminal.SetWorkingDirectory(project.RootPath);
            status.Text = $"Projet : {project.Name}";
        };
        explorer.ItemRenamed += UpdateOpenPaths;
        explorer.BeforeDelete += ConfirmOpenFilesBeforeDelete;
        var editorOutput = new SplitContainer { Size = new Size(900, 700), Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 520, BackColor = Color.FromArgb(37, 40, 48), Panel1MinSize = 180, Panel2MinSize = 100 };
        editorOutputSplit = editorOutput;
        editorOutput.Panel1.Controls.Add(tabs);
        outputTabs = new TabControl { Dock = DockStyle.Fill };
        var consolePage = new TabPage("Console");
        consolePage.Controls.Add(new ElementHost { Dock = DockStyle.Fill, Child = new OutputView { DataContext = viewModel.Output } });
        var terminalPage = new TabPage("Terminal");
        terminalPage.Controls.Add(new ElementHost { Dock = DockStyle.Fill, Child = new TerminalView { DataContext = viewModel.Terminal } });
        var errorsPage = new TabPage("Erreurs"); errorsPage.Controls.Add(diagnosticsList);
        outputTabs.TabPages.AddRange([consolePage, terminalPage, errorsPage]);
        editorOutput.Panel2.Controls.Add(outputTabs); split.Panel2.Controls.Add(editorOutput);
        viewModel.Output.SaveAllRequested += SaveAllOpenFiles;
        viewModel.Output.DiagnosticActivated += NavigateToBuildDiagnostic;
        viewModel.Output.Diagnostics.CollectionChanged += (_, _) => RefreshDiagnostics();
        viewModel.Output.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(viewModel.Output.Status)) status.Text = viewModel.Output.Status; };
        diagnosticsList.DoubleClick += (_, _) => { if (diagnosticsList.SelectedItem is DiagnosticListItem item) NavigateToDiagnostic(item); };
        tabs.SelectedIndexChanged += (_, _) => UpdateOutputTarget();
        var strip = new StatusStrip { BackColor = Color.FromArgb(0, 104, 156), ForeColor = Color.White }; strip.Items.Add(status);
        Controls.Add(split); Controls.Add(bar); Controls.Add(menu); Controls.Add(strip);
        bar.Dock = DockStyle.Top; menu.Dock = DockStyle.Top; strip.Dock = DockStyle.Bottom;
        SetTheme(AppTheme.Dark);
        NewFile();
    }

    private static void Add(ToolStripMenuItem parent, string label, EventHandler action, Keys shortcut = Keys.None)
    { var item = new ToolStripMenuItem(label) { ShortcutKeys = shortcut }; item.Click += action; parent.DropDownItems.Add(item); }
    private static void Button(ToolStrip bar, string label, Action action)
    { var item = new ToolStripButton(label); item.Click += (_, _) => action(); bar.Items.Add(item); }
    private void SetTheme(AppTheme theme)
    {
        viewModel.ThemeService.SetTheme(theme);
        viewModel.ThemeService.Apply(this);
        var renderer = new ToolStripProfessionalRenderer(new ThemeColors(theme == AppTheme.Dark));
        if (MainMenuStrip is not null) MainMenuStrip.Renderer = renderer;
        foreach (var strip in Controls.OfType<ToolStrip>()) strip.Renderer = new ToolStripProfessionalRenderer(new ThemeColors(theme == AppTheme.Dark));
    }
    private void ToggleExplorer()
    {
        if (mainSplit is null) return;
        viewModel.ExplorerVisible = !viewModel.ExplorerVisible;
        mainSplit.Panel1Collapsed = !viewModel.ExplorerVisible;
    }
    private void ToggleOutput()
    {
        if (editorOutputSplit is null) return;
        viewModel.OutputVisible = !viewModel.OutputVisible;
        editorOutputSplit.Panel2Collapsed = !viewModel.OutputVisible;
    }
    private void OpenSettings()
    {
        using var settings = new SettingsView(viewModel.ThemeService.Current);
        viewModel.ThemeService.Apply(settings);
        if (settings.ShowDialog(this) == DialogResult.OK) SetTheme(settings.SelectedTheme);
    }
    private bool SaveAllOpenFiles()
    {
        foreach (var page in tabs.TabPages.Cast<TabPage>().Where(page => EditorOf(page)?.Modified == true))
        {
            SavePage(page);
            if (EditorOf(page)?.Modified == true) return false;
        }
        return true;
    }
    private void UpdateOutputTarget() => viewModel.Output.ProjectPath = !string.IsNullOrWhiteSpace(workspace) ? workspace : PathOf();
    private void RefreshDiagnostics()
    {
        if (InvokeRequired) { BeginInvoke(RefreshDiagnostics); return; }
        diagnosticsList.BeginUpdate();
        diagnosticsList.Items.Clear();
        foreach (var item in analysisDiagnostics) diagnosticsList.Items.Add(item);
        foreach (var diagnostic in viewModel.Output.Diagnostics)
            diagnosticsList.Items.Add(new DiagnosticListItem(diagnostic.Display, diagnostic.File, diagnostic.Line, diagnostic.Column));
        diagnosticsList.EndUpdate();
    }
    private void NavigateToBuildDiagnostic(BuildDiagnostic diagnostic) => NavigateToDiagnostic(new DiagnosticListItem(diagnostic.Display, diagnostic.File, diagnostic.Line, diagnostic.Column));
    private void NavigateToDiagnostic(DiagnosticListItem diagnostic)
    {
        var path = diagnostic.File;
        if (!Path.IsPathRooted(path) && workspace is not null) path = Path.GetFullPath(Path.Combine(workspace, path));
        if (File.Exists(path)) OpenPath(path);
        var editor = Editor();
        if (editor is null) return;
        editor.SelectLine(diagnostic.Line, diagnostic.Column);
        if (outputTabs is not null) outputTabs.SelectedIndex = 2;
        status.Text = $"Erreur sélectionnée : ligne {diagnostic.Line}, colonne {diagnostic.Column}";
    }
    private CodeEditorSurface? Editor() => EditorOf(tabs.SelectedTab);
    private static CodeEditorSurface? EditorOf(TabPage? page) => page?.Controls.OfType<CodeEditorSurface>().FirstOrDefault();
    private string? PathOf(TabPage? page = null) => (page ?? tabs.SelectedTab)?.Tag as string;

    private void NewFile()
    {
        var page = new TabPage("Sans titre") { Tag = null, BackColor = Color.FromArgb(30, 33, 40) };
        var surface = new CodeEditorSurface(); surface.Modified = false;
        page.Controls.Add(surface); viewModel.ThemeService.Apply(page); tabs.TabPages.Add(page); tabs.SelectedTab = page; UpdateTitle(page);
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
            viewModel.ThemeService.Apply(page); tabs.TabPages.Add(page); tabs.SelectedTab = page; UpdateTitle(page); UpdateOutputTarget(); status.Text = path;
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
        try { fileService.WriteText(path, editor.Text); page.Tag = Path.GetFullPath(path); editor.Modified = false; UpdateTitle(page); UpdateOutputTarget(); status.Text = $"Enregistré : {path}"; if (workspace is not null) explorer.RefreshProject(); }
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

        try
        {
            var diagnostics = codeAnalysisService.Analyze(editor.Text);
            viewModel.Output.Log.Clear();
            analysisDiagnostics.Clear();
            foreach (var diagnostic in diagnostics)
                analysisDiagnostics.Add(new DiagnosticListItem($"{diagnostic.Severity} | {diagnostic.Code} | Ligne {diagnostic.Line}, Colonne {diagnostic.Column} — {diagnostic.Message}", PathOf() ?? string.Empty, diagnostic.Line, diagnostic.Column));
            RefreshDiagnostics();
            if (outputTabs is not null) outputTabs.SelectedIndex = 2;
            viewModel.Output.Log.AppendLine(diagnostics.Count == 0
                ? "Aucune erreur ni aucun avertissement détecté."
                : $"Analyse terminée : {diagnostics.Count} problème(s).", diagnostics.Count == 0 ? OutputKind.Success : OutputKind.Info);
            status.Text = $"Analyse terminée : {diagnostics.Count} problème(s).";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erreur d'analyse", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RunCurrent()
    {
        var editor = Editor(); if (editor is null) return;
        if (PathOf() is null) { SaveCurrent(); if (PathOf() is null) return; }
        SaveCurrent(); var file = PathOf()!; var ext = Path.GetExtension(file).ToLowerInvariant();
        if (outputTabs is not null) outputTabs.SelectedIndex = 0;
        if (ext == ".cs")
        {
            UpdateOutputTarget();
            viewModel.Output.RunCommand.Execute(null);
            return;
        }
        var (exe, args) = ext switch
        {
            ".py" => ("python", Quote(file)), ".js" => ("node", Quote(file)), ".ps1" => ("powershell", $"-NoProfile -ExecutionPolicy Bypass -File {Quote(file)}"),
            ".bat" or ".cmd" => ("cmd", $"/c {Quote(file)}"),
            _ => ("", "")
        };
        if (exe.Length == 0)
        {
            viewModel.Output.Log.Clear();
            viewModel.Output.Log.AppendLine($"Aucun exécuteur configuré pour {ext}. Langages pris en charge : C#, Python, JavaScript, PowerShell et Batch.", OutputKind.Error);
            return;
        }
        viewModel.Output.Log.Clear();
        viewModel.Output.Log.AppendLine($"> {exe} {args}", OutputKind.Info); status.Text = "Exécution en cours…";
        try
        {
            using var process = new Process { StartInfo = new ProcessStartInfo(exe, args) { WorkingDirectory = Path.GetDirectoryName(file)!, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
            process.Start(); var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync();
            var result = await stdout; var errors = await stderr;
            if (result.Length > 0) viewModel.Output.Log.Append(result, OutputKind.StdOut);
            if (errors.Length > 0) viewModel.Output.Log.Append(errors, OutputKind.StdErr);
            viewModel.Output.Log.AppendLine($"Terminé avec le code {process.ExitCode}.", process.ExitCode == 0 ? OutputKind.Success : OutputKind.Error);
            status.Text = $"Terminé (code {process.ExitCode})";
        }
        catch (Exception ex) { viewModel.Output.Log.AppendLine($"Erreur : {ex.Message} Vérifiez que l’outil est installé et accessible dans PATH.", OutputKind.Error); status.Text = "Échec de l’exécution"; }
    }
    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == KeyboardShortcutHelper.Run) { _ = RunCurrent(); return true; }
        if (keyData == KeyboardShortcutHelper.ToggleExplorer) { ToggleExplorer(); return true; }
        if (keyData == KeyboardShortcutHelper.ToggleOutput) { ToggleOutput(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    private sealed record DiagnosticListItem(string Display, string File, int Line, int Column)
    {
        public override string ToString() => Display;
    }

    private sealed class ThemeColors(bool dark) : ProfessionalColorTable
    {
        private Color Surface => dark ? Color.FromArgb(30, 33, 40) : Color.FromArgb(245, 247, 250);
        public override Color MenuBorder => dark ? Color.FromArgb(60, 64, 72) : Color.FromArgb(210, 215, 222);
        public override Color MenuItemSelected => dark ? Color.FromArgb(55, 60, 70) : Color.FromArgb(225, 234, 244);
        public override Color MenuItemBorder => dark ? Color.FromArgb(70, 75, 85) : Color.FromArgb(190, 205, 220);
        public override Color ToolStripDropDownBackground => Surface;
        public override Color ImageMarginGradientBegin => Surface; public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface; public override Color ToolStripGradientBegin => Surface;
        public override Color ToolStripGradientMiddle => Surface; public override Color ToolStripGradientEnd => Surface;
    }
}
