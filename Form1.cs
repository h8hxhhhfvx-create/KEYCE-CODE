using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
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
    private readonly SettingsService settingsService = new();
    private AppSettings settings = new();
    private readonly CodeAnalysisService codeAnalysisService = new();
    private readonly FormatService formatService = new();
    private readonly SearchService searchService = new();
    private readonly AiAgentService aiAgentService = new();
    private readonly LanguageRunnerService languageRunnerService = new();
    private readonly TestService testService = new();
    private readonly DebuggerService debuggerService = new();
    private readonly ExtensionService extensionService = new();
    private readonly List<DebugBreakpoint> debugBreakpoints = new();
    private GitService? gitService;
    private readonly List<(int Start, int End, int Line, int Column)> diagnosticRanges = new();
    private readonly List<(int Start, int End, string FilePath, int Line, int Column)> searchRanges = new();
    private CancellationTokenSource? liveAnalysisCancellation;
    private readonly OutputViewModel outputViewModel = new();
    private readonly TerminalViewModel terminalViewModel = new();
    private IReadOnlyList<AgentChange>? lastAgentPlan;
    private AiChatViewModel? aiChatViewModel;
    private AiChatView? aiChatView;
    private TabControl? bottomTabs;
    private MenuStrip? menuStrip;
    private ToolStripMenuItem? extensionsMenu;
    private ToolStrip? toolBar;
    private StatusStrip? statusStrip;
    private readonly RichTextBox output = new() { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.FromArgb(20, 22, 27), ForeColor = Color.Gainsboro, BorderStyle = BorderStyle.None, Font = new Font("Cascadia Mono", 10) };
    private readonly ToolStripStatusLabel status = new("Prêt") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel cursorStatus = new("Ln 1, Col 1") { TextAlign = ContentAlignment.MiddleRight };
    private string? workspace;

    public Form1()
    {
        settings = settingsService.Load();
        aiChatViewModel = new AiChatViewModel(settings);
        Text = "KeyceCode"; Width = 1280; Height = 800; MinimumSize = new Size(900, 600);
        BackColor = Color.FromArgb(37, 40, 48); ForeColor = Color.Gainsboro; StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
        output.MouseDoubleClick += Output_MouseDoubleClick;
        outputViewModel.DiagnosticActivated += OpenDiagnostic;
        debuggerService.OutputReceived += DebuggerOutputReceived;
        debuggerService.SnapshotUpdated += DebuggerSnapshotUpdated;
        FormClosing += (_, e) => { if (tabs.TabPages.Cast<TabPage>().Any(t => !CanClose(t))) e.Cancel = true; };
        FormClosed += async (_, _) => { liveAnalysisCancellation?.Cancel(); outputViewModel.Stop(); terminalViewModel.Stop(); await debuggerService.DisposeAsync(); };
    }

    private void BuildLayout()
    {
        var menu = new MenuStrip { BackColor = Color.FromArgb(30, 33, 40), ForeColor = Color.Gainsboro, Font = new Font("Segoe UI", 9F), Padding = new Padding(6, 3, 6, 3), Renderer = new ToolStripProfessionalRenderer(new DarkColors()) };
        menuStrip = menu;
        var fileMenu = new ToolStripMenuItem("Fichier");
        Add(fileMenu, "Nouveau", (_, _) => NewFile(), Keys.Control | Keys.N);
        Add(fileMenu, "Ouvrir…", (_, _) => OpenFile(), Keys.Control | Keys.O);
        Add(fileMenu, "Ouvrir un projet C#…", (_, _) => OpenProject());
        Add(fileMenu, "Ouvrir un dossier de projet…", (_, _) => OpenWorkspace());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        Add(fileMenu, "Enregistrer", (_, _) => SaveCurrent(), Keys.Control | Keys.S);
        Add(fileMenu, "Enregistrer sous…", (_, _) => SaveAs(), Keys.Control | Keys.Shift | Keys.S);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        Add(fileMenu, "Fermer l’onglet", (_, _) => CloseCurrent(), Keys.Control | Keys.W);
        Add(fileMenu, "Quitter", (_, _) => Close());
        var editMenu = new ToolStripMenuItem("Édition");
        Add(editMenu, "Annuler", (_, _) => Editor()?.Undo(), Keys.Control | Keys.Z);
        Add(editMenu, "Rétablir", (_, _) => Editor()?.Redo(), Keys.Control | Keys.Y);
        Add(editMenu, "Couper", (_, _) => Editor()?.Cut(), Keys.Control | Keys.X);
        Add(editMenu, "Copier", (_, _) => Editor()?.Copy(), Keys.Control | Keys.C);
        Add(editMenu, "Coller", (_, _) => Editor()?.Paste(), Keys.Control | Keys.V);
        Add(editMenu, "Rechercher…", (_, _) => FindText(), Keys.Control | Keys.F);
        Add(editMenu, "Remplacer…", (_, _) => ReplaceText(), Keys.Control | Keys.H);
        Add(editMenu, "Aller à la ligne…", (_, _) => GoToLine(), Keys.Control | Keys.G);
        Add(editMenu, "Formater le document", (_, _) => FormatCurrent(), Keys.Control | Keys.Shift | Keys.I);
        Add(editMenu, "Rechercher dans les fichiers…", (_, _) => SearchInFiles(), Keys.Control | Keys.Shift | Keys.F);
        var viewMenu = new ToolStripMenuItem("Affichage");
        Add(viewMenu, "Thème sombre", (_, _) => SetTheme("Dark"));
        Add(viewMenu, "Thème clair", (_, _) => SetTheme("Light"));
        Add(viewMenu, "Augmenter la taille du texte", (_, _) => AdjustEditorFontSize(1));
        Add(viewMenu, "Réduire la taille du texte", (_, _) => AdjustEditorFontSize(-1));
        Add(viewMenu, "Activer/désactiver le retour à la ligne", (_, _) => ToggleWordWrap());
        Add(viewMenu, "Tabulations : 2 espaces", (_, _) => SetTabSize(2));
        Add(viewMenu, "Tabulations : 4 espaces", (_, _) => SetTabSize(4));
        Add(viewMenu, "Tabulations : 8 espaces", (_, _) => SetTabSize(8));
        var runMenu = new ToolStripMenuItem("Exécuter"); Add(runMenu, "Lancer le fichier", async (_, _) => await RunCurrent(), Keys.F5);
        Add(runMenu, "Lancer les tests du projet", async (_, _) => await RunTestsAsync());
        Add(runMenu, "Ajouter/retirer un point d’arrêt", (_, _) => ToggleBreakpoint(), Keys.F9);
        Add(runMenu, "Démarrer le débogage", async (_, _) => await StartDebuggingAsync(), Keys.F5 | Keys.Shift);
        Add(runMenu, "Arrêter le débogage", async (_, _) => await StopDebuggingAsync());
        Add(runMenu, "Continuer le débogage", async (_, _) => await ContinueDebuggingAsync(), Keys.F5);
        Add(runMenu, "Pas à pas principal", async (_, _) => await StepDebuggingAsync("over"), Keys.F10);
        Add(runMenu, "Entrer dans la fonction", async (_, _) => await StepDebuggingAsync("into"), Keys.F11);
        Add(runMenu, "Sortir de la fonction", async (_, _) => await StepDebuggingAsync("out"), Keys.Shift | Keys.F11);
        var gitMenu = new ToolStripMenuItem("Git");
        Add(gitMenu, "Actualiser l’état", async (_, _) => await RefreshGitStatusAsync());
        Add(gitMenu, "Créer un commit…", async (_, _) => await CommitChangesAsync());
        Add(gitMenu, "Tirer les changements", async (_, _) => await PullGitAsync());
        Add(gitMenu, "Envoyer les changements", async (_, _) => await PushGitAsync());
        Add(gitMenu, "Lister les branches", async (_, _) => await ListGitBranchesAsync());
        Add(gitMenu, "Créer une branche…", async (_, _) => await CreateGitBranchAsync());
        Add(gitMenu, "Changer de branche…", async (_, _) => await CheckoutGitBranchAsync());
        var aiMenu = new ToolStripMenuItem("IA");
        Add(aiMenu, "Configurer Ollama…", (_, _) => ConfigureOllama());
        Add(aiMenu, "Agent : proposer une modification…", async (_, _) => await GenerateAgentPlanAsync());
        Add(aiMenu, "Agent : annuler la dernière modification", async (_, _) => await RestoreAgentPlanAsync());
        var extensionMenu = new ToolStripMenuItem("Extensions");
        extensionsMenu = extensionMenu;
        Add(extensionMenu, "Actualiser les extensions", (_, _) => RefreshExtensions());
        Add(extensionMenu, "Ouvrir le dossier des extensions", (_, _) => OpenExtensionsFolder());
        var helpMenu = new ToolStripMenuItem("Aide");
        Add(helpMenu, "Raccourcis clavier", (_, _) => ShowShortcuts());
        Add(helpMenu, "À propos de KEYCE CODE", (_, _) => ShowAbout());
        menu.Items.AddRange([fileMenu, editMenu, viewMenu, runMenu, gitMenu, aiMenu, extensionMenu, helpMenu]); MainMenuStrip = menu;

        var bar = new ToolStrip { BackColor = Color.FromArgb(30, 33, 40), ForeColor = Color.Gainsboro, GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(6, 3, 6, 3), Height = 34, Renderer = new ToolStripProfessionalRenderer(new DarkColors()) };
        toolBar = bar;
        Button(bar, "＋ Nouveau", () => NewFile()); Button(bar, "Ouvrir", OpenFile); Button(bar, "Projet C#", OpenProject); Button(bar, "Dossier", OpenWorkspace); Button(bar, "Enregistrer", SaveCurrent);
        bar.Items.Add(new ToolStripSeparator()); Button(bar, "▶ Exécuter (F5)", async () => await RunCurrent());
        Button(bar, "🔨 Compiler", CompileProject); Button(bar, "▶ Projet", RunProject);
        Button(bar, "🧪 Tests", async () => await RunTestsAsync()); Button(bar, "🔍 Analyser", AnalyzeCurrent);

        var split = new SplitContainer { Size = new Size(1200, 700), Dock = DockStyle.Fill, SplitterDistance = 260, BackColor = Color.FromArgb(37, 40, 48), Panel1MinSize = 180, Panel2MinSize = 400 };
        split.Panel1.Controls.Add(explorer);
        explorer.FileOpenRequested += OpenPath;
        explorer.ProjectOpened += project =>
        {
            workspace = project.RootPath;
            SetProjectContext(project.RootPath);
            status.Text = $"Projet : {project.Name}";
        };
        explorer.ItemRenamed += UpdateOpenPaths;
        explorer.BeforeDelete += ConfirmOpenFilesBeforeDelete;
        // Le panneau inférieur contient plusieurs vues (console, terminal, IA) dont
        // les barres d'actions et les champs de saisie doivent rester entièrement
        // visibles, même lorsque la fenêtre n'est pas maximisée.
        var editorOutput = new SplitContainer { Size = new Size(900, 700), Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 520, BackColor = Color.FromArgb(37, 40, 48), Panel1MinSize = 180, Panel2MinSize = 220 };
        editorOutput.Panel1.Controls.Add(tabs);
        var outputPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 22, 27) };
        outputPanel.Controls.Add(output);
        outputPanel.Controls.Add(new Label { Text = "  SORTIE", Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Silver, BackColor = Color.FromArgb(30, 33, 40), Font = new Font("Segoe UI", 9, FontStyle.Bold) });
        bottomTabs = new TabControl { Dock = DockStyle.Fill, MinimumSize = new Size(0, 220) };
        bottomTabs.TabPages.Add(new TabPage("Sortie") { Controls = { outputPanel } });
        bottomTabs.TabPages.Add(CreateWpfTab("Compilation", new OutputView { DataContext = outputViewModel }));
        bottomTabs.TabPages.Add(CreateWpfTab("Terminal", new TerminalView { DataContext = terminalViewModel }));
        aiChatView = new AiChatView { DataContext = aiChatViewModel };
        bottomTabs.TabPages.Add(CreateWpfTab("Assistant IA", aiChatView));
        editorOutput.Panel2.Controls.Add(bottomTabs); split.Panel2.Controls.Add(editorOutput);
        var strip = new StatusStrip { BackColor = Color.FromArgb(0, 104, 156), ForeColor = Color.White, Font = new Font("Segoe UI", 9F) }; strip.Items.Add(status);
        strip.Items.Add(cursorStatus);
        statusStrip = strip;
        Controls.Add(split); Controls.Add(bar); Controls.Add(menu); Controls.Add(strip);
        bar.Dock = DockStyle.Top; menu.Dock = DockStyle.Top; strip.Dock = DockStyle.Bottom;
        ApplyChromeTheme();
        tabs.SelectedIndexChanged += (_, _) => { UpdateCursorStatus(Editor()); UpdateAiContext(); };
        NewFile();
        RefreshExtensions();
    }

    private static TabPage CreateWpfTab(string title, System.Windows.Controls.UserControl view)
    {
        var host = new ElementHost { Dock = DockStyle.Fill, Child = view };
        var page = new TabPage(title);
        page.Controls.Add(host);
        return page;
    }

    private void SetProjectContext(string path)
    {
        outputViewModel.ProjectPath = path;
        var directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            terminalViewModel.SetWorkingDirectory(directory);
            gitService = new GitService(directory);
        }
    }

    private async Task RefreshGitStatusAsync()
    {
        if (gitService is null) { status.Text = "Aucun dépôt Git ouvert."; return; }
        try
        {
            var snapshot = await gitService.GetStatusAsync();
            SelectBottomTab("Sortie");
            output.Clear();
            output.AppendText($"Branche : {snapshot.Branch}\r\n");
            output.AppendText(snapshot.Files.Count == 0
                ? "Dépôt propre.\r\n"
                : string.Join("\r\n", snapshot.Files.Select(file => file.Display)) + "\r\n");
            status.Text = $"Git : {snapshot.Branch}, {snapshot.Files.Count} fichier(s) modifié(s).";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void RefreshExtensions()
    {
        if (extensionsMenu is null) return;
        while (extensionsMenu.DropDownItems.Count > 2) extensionsMenu.DropDownItems.RemoveAt(2);
        try
        {
            var loaded = extensionService.Load(workspace);
            foreach (var extension in loaded)
            {
                var item = new ToolStripMenuItem(extension.Loaded ? $"✓ {extension.Name}" : $"⚠ {extension.Name}") { ToolTipText = extension.Error ?? extension.AssemblyPath };
                extensionsMenu.DropDownItems.Add(item);
            }
            foreach (var command in extensionService.Commands)
            {
                var item = new ToolStripMenuItem(command.Label);
                item.Click += (_, _) => command.Action();
                extensionsMenu.DropDownItems.Add(item);
            }
            status.Text = loaded.Count == 0 ? "Aucune extension installée." : $"Extensions : {loaded.Count} détectée(s).";
        }
        catch (Exception ex) { status.Text = "Erreur extensions : " + ex.Message; }
    }

    private void OpenExtensionsFolder()
    {
        try
        {
            Directory.CreateDirectory(extensionService.ExtensionsDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", Quote(extensionService.ExtensionsDirectory)) { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Extensions", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task CommitChangesAsync()
    {
        if (gitService is null) { status.Text = "Aucun dépôt Git ouvert."; return; }
        var message = Microsoft.VisualBasic.Interaction.InputBox("Message du commit :", "Git — créer un commit");
        if (string.IsNullOrWhiteSpace(message)) return;
        try { await gitService.StageAsync(["."]); await gitService.CommitAsync(message); await RefreshGitStatusAsync(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task PullGitAsync()
    {
        if (gitService is null) { status.Text = "Aucun dépôt Git ouvert."; return; }
        try { await gitService.PullAsync(); await RefreshGitStatusAsync(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task PushGitAsync()
    {
        if (gitService is null) { status.Text = "Aucun dépôt Git ouvert."; return; }
        try { await gitService.PushAsync(); await RefreshGitStatusAsync(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task ListGitBranchesAsync()
    {
        if (gitService is null) { status.Text = "Aucun dépôt Git ouvert."; return; }
        try
        {
            var branches = await gitService.GetBranchesAsync();
            SelectBottomTab("Sortie");
            output.Clear();
            output.AppendText(string.Join("\r\n", branches.Select(branch => $"{(branch.IsCurrent ? "*" : " ")} {branch.Name}")) + "\r\n");
            status.Text = $"Git : {branches.Count} branche(s).";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task CreateGitBranchAsync()
    {
        if (gitService is null) { status.Text = "Aucun dépôt Git ouvert."; return; }
        var name = Microsoft.VisualBasic.Interaction.InputBox("Nom de la nouvelle branche :", "Git — créer une branche");
        if (string.IsNullOrWhiteSpace(name)) return;
        try { await gitService.CreateBranchAsync(name); await RefreshGitStatusAsync(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task CheckoutGitBranchAsync()
    {
        if (gitService is null) { status.Text = "Aucun dépôt Git ouvert."; return; }
        try
        {
            var branches = await gitService.GetBranchesAsync();
            var name = Microsoft.VisualBasic.Interaction.InputBox(
                "Branche cible (branches disponibles : " + string.Join(", ", branches.Select(branch => branch.Name)) + ") :",
                "Git — changer de branche");
            if (string.IsNullOrWhiteSpace(name)) return;
            await gitService.CheckoutAsync(name);
            await RefreshGitStatusAsync();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task GenerateAgentPlanAsync()
    {
        var root = workspace ?? (PathOf() is { } file ? Path.GetDirectoryName(file) : null);
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            MessageBox.Show(this, "Ouvrez un dossier ou un projet avant d’utiliser l’agent.", "Agent IA", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var instruction = Microsoft.VisualBasic.Interaction.InputBox(
            "Décrivez la modification souhaitée. L’agent proposera un aperçu avant toute écriture :",
            "Agent IA — nouvelle proposition");
        if (string.IsNullOrWhiteSpace(instruction)) return;

        try
        {
            status.Text = "Agent IA : génération de la proposition…";
            var plan = await aiAgentService.GeneratePlanAsync(
                settings.OllamaEndpoint,
                settings.OllamaModel,
                root,
                instruction,
                Editor()?.Text);
            SelectBottomTab("Sortie");
            output.Clear();
            output.AppendText("PROPOSITION DE L’AGENT IA\r\n\r\n");
            foreach (var change in plan)
            {
                output.AppendText($"Fichier : {change.RelativePath} ({(change.ExistingContent is null ? "création" : "modification")})\r\n");
                output.AppendText(change.Content + "\r\n\r\n");
            }

            var answer = MessageBox.Show(
                this,
                $"L’agent propose {plan.Count} fichier(s). Voulez-vous appliquer ces modifications ?\n\nUn aperçu est affiché dans le panneau Sortie.",
                "Agent IA — confirmation obligatoire",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) { status.Text = "Proposition de l’agent refusée."; return; }
            await aiAgentService.ApplyPlanAsync(root, plan);
            lastAgentPlan = plan;
            explorer.RefreshProject();
            status.Text = $"Agent IA : {plan.Count} fichier(s) modifié(s).";
        }
        catch (Exception ex)
        {
            status.Text = "Agent IA : échec.";
            MessageBox.Show(this, ex.Message, "Agent IA", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RestoreAgentPlanAsync()
    {
        var root = workspace ?? (PathOf() is { } file ? Path.GetDirectoryName(file) : null);
        if (string.IsNullOrWhiteSpace(root) || lastAgentPlan is null)
        {
            status.Text = "Aucune modification récente de l’agent à annuler.";
            return;
        }
        try
        {
            await aiAgentService.RestoreLastPlanAsync(root);
            lastAgentPlan = null;
            explorer.RefreshProject();
            status.Text = "Dernière modification de l’agent annulée.";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Agent IA", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private static void Add(ToolStripMenuItem parent, string label, EventHandler action, Keys shortcut = Keys.None)
    { var item = new ToolStripMenuItem(label) { ShortcutKeys = shortcut }; item.Click += action; parent.DropDownItems.Add(item); }
    private static void Button(ToolStrip bar, string label, Action action)
    {
        var item = new ToolStripButton(label)
        {
            AutoSize = true,
            Height = 27,
            Padding = new Padding(8, 0, 8, 0),
            Margin = new Padding(1, 0, 1, 0),
            DisplayStyle = ToolStripItemDisplayStyle.Text
        };
        item.Click += (_, _) => action();
        bar.Items.Add(item);
    }
    private CodeEditorSurface? Editor() => EditorOf(tabs.SelectedTab);
    private static CodeEditorSurface? EditorOf(TabPage? page) => page?.Controls.OfType<CodeEditorSurface>().FirstOrDefault();
    private string? PathOf(TabPage? page = null) => (page ?? tabs.SelectedTab)?.Tag as string;

    private void NewFile()
    {
        var page = new TabPage("Sans titre") { Tag = null, BackColor = Color.FromArgb(30, 33, 40) };
        var surface = new CodeEditorSurface(); AttachEditor(surface); ApplyEditorSettings(surface); surface.Modified = false;
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
            var surface = new CodeEditorSurface(); page.Controls.Add(surface); AttachEditor(surface); ApplyEditorSettings(surface); surface.ApplyLanguage(Path.GetExtension(path)); surface.Text = fileService.ReadText(path); surface.Modified = false;
            tabs.TabPages.Add(page); tabs.SelectedTab = page; UpdateTitle(page); SetProjectContext(workspace ?? path); status.Text = path;
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

    private void ApplyEditorSettings(CodeEditorSurface surface) => surface.ApplyAppearance(settings);

    private void AttachEditor(CodeEditorSurface surface)
    {
        surface.CaretPositionChanged += (_, _) => UpdateCursorStatus(surface);
    }

    private void UpdateCursorStatus(CodeEditorSurface? surface)
    {
        if (surface is null) return;
        var (line, column) = surface.CaretPosition;
        cursorStatus.Text = $"Ln {line}, Col {column}";
    }

    private void SetTheme(string theme)
    {
        settings.Theme = theme;
        settingsService.Save(settings);
        ApplyChromeTheme();
        foreach (var surface in tabs.TabPages.Cast<TabPage>().SelectMany(page => page.Controls.OfType<CodeEditorSurface>()))
            ApplyEditorSettings(surface);
        status.Text = $"Thème {theme switch { "Dark" => "sombre", _ => "clair" }} activé.";
    }

    private void ApplyChromeTheme()
    {
        var dark = !string.Equals(settings.Theme, "Light", StringComparison.OrdinalIgnoreCase);
        var chrome = dark ? Color.FromArgb(30, 33, 40) : Color.FromArgb(232, 235, 240);
        var surface = dark ? Color.FromArgb(37, 40, 48) : Color.FromArgb(248, 249, 251);
        var text = dark ? Color.Gainsboro : Color.FromArgb(32, 35, 40);

        BackColor = surface;
        ForeColor = text;
        if (menuStrip is not null)
        {
            menuStrip.BackColor = chrome;
            menuStrip.ForeColor = text;
            menuStrip.Renderer = new ToolStripProfessionalRenderer(dark ? new DarkColors() : new LightColors());
        }
        if (toolBar is not null)
        {
            toolBar.BackColor = chrome;
            toolBar.ForeColor = text;
            toolBar.Renderer = new ToolStripProfessionalRenderer(dark ? new DarkColors() : new LightColors());
        }
        if (statusStrip is not null)
        {
            statusStrip.BackColor = dark ? Color.FromArgb(0, 104, 156) : Color.FromArgb(30, 120, 175);
            statusStrip.ForeColor = Color.White;
        }

        ApplyWinFormsColors(this, surface, text);
        explorer.ApplyTheme(dark);
        aiChatView?.ApplyTheme(dark);
        output.BackColor = dark ? Color.FromArgb(20, 22, 27) : Color.White;
        output.ForeColor = dark ? Color.Gainsboro : Color.FromArgb(32, 35, 40);
    }

    private static void ApplyWinFormsColors(Control parent, Color surface, Color text)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is MenuStrip or ToolStrip or StatusStrip) continue;
            if (child is Panel or TabControl or TabPage or SplitContainer) child.BackColor = surface;
            child.ForeColor = text;
            if (child.HasChildren) ApplyWinFormsColors(child, surface, text);
        }
    }

    private void AdjustEditorFontSize(double amount)
    {
        settings.EditorFontSize = Math.Clamp(settings.EditorFontSize + amount, 8, 32);
        settingsService.Save(settings);
        foreach (var surface in tabs.TabPages.Cast<TabPage>().SelectMany(page => page.Controls.OfType<CodeEditorSurface>()))
            ApplyEditorSettings(surface);
        status.Text = $"Taille du texte : {settings.EditorFontSize:0} pt";
    }

    private void ToggleWordWrap()
    {
        settings.WordWrap = !settings.WordWrap;
        settingsService.Save(settings);
        foreach (var surface in tabs.TabPages.Cast<TabPage>().SelectMany(page => page.Controls.OfType<CodeEditorSurface>()))
            ApplyEditorSettings(surface);
        status.Text = settings.WordWrap ? "Retour à la ligne activé." : "Retour à la ligne désactivé.";
    }

    private void SetTabSize(int size)
    {
        settings.TabSize = size;
        settingsService.Save(settings);
        foreach (var surface in tabs.TabPages.Cast<TabPage>().SelectMany(page => page.Controls.OfType<CodeEditorSurface>()))
            ApplyEditorSettings(surface);
        status.Text = $"Tabulations : {size} espaces.";
    }

    private void ShowShortcuts()
    {
        MessageBox.Show(this,
            "Ctrl+N  Nouveau fichier\nCtrl+O  Ouvrir\nCtrl+S  Enregistrer\nCtrl+Maj+S  Enregistrer sous\nCtrl+W  Fermer l’onglet\nCtrl+Z  Annuler\nCtrl+Y  Rétablir\nCtrl+F  Rechercher\nCtrl+H  Remplacer\nCtrl+G  Aller à la ligne\nCtrl+Maj+I  Formater le document\nCtrl++ / Ctrl+-  Zoomer\nCtrl+0  Taille normale\nCtrl+Maj+F  Rechercher dans les fichiers\nCtrl+Espace  Auto-complétion C#\nF5  Exécuter",
            "Raccourcis clavier — KEYCE CODE",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ShowAbout()
    {
        MessageBox.Show(this,
            "KEYCE CODE\nÉditeur C# pour Windows\n\nÉdition, coloration syntaxique, compilation et terminal intégrés.",
            "À propos de KEYCE CODE",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ConfigureOllama()
    {
        var endpoint = Microsoft.VisualBasic.Interaction.InputBox(
            "Adresse du serveur Ollama :", "Configuration Ollama", settings.OllamaEndpoint);
        if (string.IsNullOrWhiteSpace(endpoint)) return;

        var model = Microsoft.VisualBasic.Interaction.InputBox(
            "Nom du modèle Ollama :", "Configuration Ollama", settings.OllamaModel);
        if (string.IsNullOrWhiteSpace(model)) return;

        settings.OllamaEndpoint = endpoint.Trim().TrimEnd('/');
        settings.OllamaModel = model.Trim();
        settingsService.Save(settings);
        status.Text = $"Ollama configuré : {settings.OllamaModel}";
    }

    private void SaveAllOpenFiles()
    {
        foreach (var page in tabs.TabPages.Cast<TabPage>().ToList())
            if (EditorOf(page)?.Modified == true) SavePage(page);
    }

    private void CompileProject()
    {
        if (!TrySetProjectPath()) return;
        SelectBottomTab("Compilation");
        if (outputViewModel.CompileCommand.CanExecute(null)) outputViewModel.CompileCommand.Execute(null);
    }

    private async Task RunTestsAsync()
    {
        var root = workspace ?? (PathOf() is { } file ? Path.GetDirectoryName(file) : null);
        if (string.IsNullOrWhiteSpace(root))
        {
            MessageBox.Show(this, "Ouvrez un projet ou un dossier avant de lancer les tests.", "Tests", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            SelectBottomTab("Sortie");
            output.Clear();
            output.AppendText("> dotnet test\r\n\r\n");
            status.Text = "Tests en cours…";
            var result = await testService.RunAsync(root);
            output.AppendText(result.Output);
            output.AppendText($"\r\nRésultat : {result.Passed} réussi(s), {result.Failed} échec(s), {result.Skipped} ignoré(s).\r\n");
            status.Text = result.Success ? $"Tests réussis : {result.Passed}." : $"Tests échoués : {result.Failed}.";
        }
        catch (Exception ex)
        {
            output.AppendText("\r\nErreur : " + ex.Message);
            status.Text = "Échec des tests.";
        }
    }

    private void ToggleBreakpoint()
    {
        var file = PathOf();
        var editor = Editor();
        if (file is null || editor is null) { status.Text = "Ouvrez un fichier avant de placer un point d’arrêt."; return; }
        var line = editor.CaretPosition.Line;
        var existing = debugBreakpoints.FirstOrDefault(item => string.Equals(item.FilePath, file, StringComparison.OrdinalIgnoreCase) && item.Line == line);
        if (existing is not null) debugBreakpoints.Remove(existing);
        else debugBreakpoints.Add(new DebugBreakpoint(file, line));
        status.Text = existing is null ? $"Point d’arrêt ajouté : ligne {line}." : $"Point d’arrêt retiré : ligne {line}.";
    }

    private async Task StartDebuggingAsync()
    {
        var path = workspace ?? PathOf();
        if (string.IsNullOrWhiteSpace(path)) { status.Text = "Ouvrez un projet avant de déboguer."; return; }
        try
        {
            var project = Directory.Exists(path) ? Directory.EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories).FirstOrDefault() : path;
            if (string.IsNullOrWhiteSpace(project)) throw new FileNotFoundException("Aucun projet .csproj trouvé.");
            var directory = Path.GetDirectoryName(project)!;
            var projectName = Path.GetFileNameWithoutExtension(project);
            var build = await ProcessRunner.RunAsync("dotnet", $"build {Quote(project)} --nologo --verbosity quiet", directory);
            if (build.ExitCode != 0) { SelectBottomTab("Sortie"); output.Text = build.Output; status.Text = "Débogage impossible : compilation échouée."; return; }
            var dll = Directory.EnumerateFiles(Path.Combine(directory, "bin"), projectName + ".dll", SearchOption.AllDirectories)
                .Where(file => !file.Contains(Path.DirectorySeparatorChar + "ref" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
                ?? throw new FileNotFoundException("Assembly compilée introuvable.");
            await debuggerService.StartAsync(dll, directory, debugBreakpoints);
            status.Text = $"Débogage démarré ({debugBreakpoints.Count} point(s) d’arrêt).";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Débogueur", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task StopDebuggingAsync()
    {
        await debuggerService.StopAsync();
        status.Text = "Débogage arrêté.";
    }

    private async Task ContinueDebuggingAsync()
    {
        try { await debuggerService.ContinueAsync(); status.Text = "Débogage poursuivi."; }
        catch (Exception ex) { status.Text = ex.Message; }
    }

    private async Task StepDebuggingAsync(string mode)
    {
        try
        {
            if (mode == "into") await debuggerService.StepIntoAsync();
            else if (mode == "out") await debuggerService.StepOutAsync();
            else await debuggerService.StepOverAsync();
            status.Text = "Pas à pas demandé.";
        }
        catch (Exception ex) { status.Text = ex.Message; }
    }

    private void DebuggerOutputReceived(string text)
    {
        if (IsDisposed) return;
        BeginInvoke(() => { SelectBottomTab("Sortie"); output.AppendText(text); });
    }

    private void DebuggerSnapshotUpdated(DebugSnapshot snapshot)
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            SelectBottomTab("Sortie");
            output.AppendText($"\r\n--- Débogueur : thread {snapshot.ThreadId} arrêté ---\r\n");
            foreach (var frame in snapshot.Frames)
                output.AppendText($"{frame.Name} — {frame.FilePath ?? "fichier inconnu"}({frame.Line},{frame.Column})\r\n");
            if (snapshot.Variables.Count > 0)
            {
                output.AppendText("Variables locales :\r\n");
                foreach (var variable in snapshot.Variables)
                    output.AppendText($"  {variable.Name} = {variable.Value}{(variable.Type is null ? "" : $" ({variable.Type})")}\r\n");
            }
            status.Text = "Programme arrêté sur un point d’arrêt.";
        });
    }

    private void RunProject()
    {
        if (!TrySetProjectPath()) return;
        SelectBottomTab("Compilation");
        if (outputViewModel.RunCommand.CanExecute(null)) outputViewModel.RunCommand.Execute(null);
    }

    private void SelectBottomTab(string title)
    {
        if (bottomTabs is null) return;
        var page = bottomTabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(candidate => string.Equals(candidate.Text, title, StringComparison.Ordinal));
        if (page is not null) bottomTabs.SelectedTab = page;
    }

    private bool TrySetProjectPath()
    {
        var path = workspace ?? PathOf();
        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show(this, "Ouvrez un projet, un dossier ou un fichier avant de compiler.", "KEYCE CODE", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        SetProjectContext(path);
        outputViewModel.SaveAllRequested -= SaveAllOpenFiles;
        outputViewModel.SaveAllRequested += SaveAllOpenFiles;
        return true;
    }
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
        try { fileService.WriteText(path, editor.Text); page.Tag = Path.GetFullPath(path); editor.Modified = false; UpdateTitle(page); status.Text = $"Enregistré : {path}"; if (workspace is not null) explorer.RefreshProject(); }
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
        if (ReferenceEquals(page, tabs.SelectedTab) && aiChatViewModel is not null)
            aiChatViewModel.ContextText = editor.Text;
        ScheduleLiveAnalysis(editor);
    }

    private void ScheduleLiveAnalysis(CodeEditorSurface editor)
    {
        var extension = Path.GetExtension(PathOf() ?? ".cs");
        if (!string.IsNullOrEmpty(extension) && !string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase)) return;
        liveAnalysisCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        liveAnalysisCancellation = cancellation;
        _ = AnalyzeAfterPauseAsync(editor, cancellation.Token);
    }

    private async Task AnalyzeAfterPauseAsync(CodeEditorSurface editor, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(650, cancellationToken);
            var diagnostics = await Task.Run(() => codeAnalysisService.Analyze(editor.Text), cancellationToken);
            if (cancellationToken.IsCancellationRequested || !ReferenceEquals(editor, Editor())) return;
            BeginInvoke(() =>
            {
                if (!cancellationToken.IsCancellationRequested && ReferenceEquals(editor, Editor()))
                    status.Text = diagnostics.Count == 0
                        ? "Analyse en direct : aucune erreur détectée."
                        : $"Analyse en direct : {diagnostics.Count} problème(s). Utilisez Analyser pour le détail.";
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception) { /* L’analyse manuelle reste disponible si le code est momentanément incomplet. */ }
    }

    private void UpdateAiContext()
    {
        if (aiChatViewModel is not null && Editor() is { } editor)
            aiChatViewModel.ContextText = editor.Text;
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

    private void ReplaceText()
    {
        var editor = Editor();
        if (editor is null) return;
        var search = Microsoft.VisualBasic.Interaction.InputBox("Texte à rechercher :", "Remplacer");
        if (search.Length == 0) return;
        var replacement = Microsoft.VisualBasic.Interaction.InputBox("Remplacer par :", "Remplacer");
        var count = 0;
        var source = editor.Text;
        for (var index = 0; (index = source.IndexOf(search, index, StringComparison.CurrentCultureIgnoreCase)) >= 0; index += search.Length)
            count++;
        if (count == 0)
        {
            status.Text = "Aucun résultat à remplacer.";
            return;
        }

        editor.ReplaceText(source.Replace(search, replacement, StringComparison.CurrentCultureIgnoreCase));
        status.Text = $"{count} occurrence(s) remplacée(s).";
    }

    private void GoToLine()
    {
        var editor = Editor();
        if (editor is null) return;
        var value = Microsoft.VisualBasic.Interaction.InputBox("Numéro de ligne :", "Aller à la ligne", "1");
        if (!int.TryParse(value, out var line) || line < 1)
        {
            status.Text = "Numéro de ligne invalide.";
            return;
        }

        var position = GetLineStartOffset(editor.Text, line);
        editor.Select(Math.Min(position, editor.Text.Length), 0);
        status.Text = $"Ligne {line} sélectionnée.";
    }

    private void FormatCurrent()
    {
        var editor = Editor();
        if (editor is null) return;
        var extension = Path.GetExtension(PathOf() ?? ".cs");
        if (!string.IsNullOrEmpty(extension) && !string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase))
        {
            status.Text = "Le formatage automatique est disponible pour C# uniquement.";
            return;
        }

        try
        {
            var formatted = formatService.FormatCSharp(editor.Text);
            if (!string.Equals(formatted, editor.Text, StringComparison.Ordinal))
                editor.ReplaceText(formatted);
            status.Text = "Document C# formaté.";
        }
        catch (Exception ex)
        {
            status.Text = "Formatage impossible : " + ex.Message;
        }
    }

    private void SearchInFiles()
    {
        var root = workspace ?? (PathOf() is { } file ? Path.GetDirectoryName(file) : null);
        if (string.IsNullOrWhiteSpace(root))
        {
            MessageBox.Show(this, "Ouvrez un dossier ou un fichier avant de rechercher dans les fichiers.", "Recherche", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var query = Microsoft.VisualBasic.Interaction.InputBox("Texte à rechercher dans le projet :", "Rechercher dans les fichiers");
        if (string.IsNullOrEmpty(query)) return;

        try
        {
            var results = searchService.Search(root, query);
            SelectBottomTab("Sortie");
            output.Clear();
            searchRanges.Clear();
            if (results.Count == 0)
            {
                output.Text = $"Aucun résultat pour « {query} ».";
                status.Text = "Recherche terminée : aucun résultat.";
                return;
            }

            foreach (var result in results)
            {
                var start = output.TextLength;
                output.AppendText($"{result.FilePath}({result.Line},{result.Column}) : {result.Text.Trim()}\r\n");
                searchRanges.Add((start, output.TextLength, result.FilePath, result.Line, result.Column));
            }
            status.Text = $"Recherche terminée : {results.Count} résultat(s).";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erreur de recherche", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AnalyzeCurrent()
    {
        var editor = Editor();
        if (editor is null) return;

        try
        {
            var diagnostics = codeAnalysisService.Analyze(editor.Text);
            SelectBottomTab("Sortie");
            output.Clear();
            diagnosticRanges.Clear();
            searchRanges.Clear();

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
        if (diagnostic.Line != 0)
        {
            SelectEditorPosition(diagnostic.Line, diagnostic.Column);
            return;
        }

        var search = searchRanges.FirstOrDefault(s => index >= s.Start && index < s.End);
        if (string.IsNullOrWhiteSpace(search.FilePath)) return;
        OpenPath(search.FilePath);
        SelectEditorPosition(search.Line, search.Column);
    }

    private void SelectEditorPosition(int line, int column)
    {
        if (line <= 0) return;

        var editor = Editor();
        if (editor is null) return;

        var position = GetLineStartOffset(editor.Text, line) + Math.Max(0, column - 1);
        editor.Select(Math.Min(position, editor.Text.Length), 0);
        editor.Focus();
        status.Text = $"Position sélectionnée : ligne {line}, colonne {column}";
    }

    private void OpenDiagnostic(BuildDiagnostic diagnostic)
    {
        var file = diagnostic.File;
        if (!Path.IsPathRooted(file) && !string.IsNullOrWhiteSpace(workspace))
            file = Path.Combine(workspace, file);
        if (File.Exists(file)) OpenPath(file);

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

    private async Task RunCurrent()
    {
        var editor = Editor(); if (editor is null) return;
        if (PathOf() is null) { SaveCurrent(); if (PathOf() is null) return; }
        SaveCurrent(); var file = PathOf()!; var ext = Path.GetExtension(file).ToLowerInvariant();
        if (ext == ".cs" && TrySetProjectPath())
        {
            SelectBottomTab("Compilation");
            if (outputViewModel.RunCommand.CanExecute(null)) outputViewModel.RunCommand.Execute(null);
            return;
        }

        var command = languageRunnerService.Resolve(file);
        var (exe, args) = command is null ? ("", "") : (command.Executable, command.Arguments);
        if (exe.Length == 0)
        {
            output.Text = $"Aucun exécuteur configuré pour {ext}.\r\nExtensions prises en charge : {string.Join(", ", languageRunnerService.SupportedExtensions.OrderBy(value => value))}.";
            return;
        }
        output.Clear(); output.AppendText($"> {exe} {args}\r\n\r\n"); status.Text = "Exécution en cours…";
        try
        {
            using var process = new Process { StartInfo = new ProcessStartInfo(exe, args) { WorkingDirectory = Path.GetDirectoryName(file)!, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
            process.Start(); var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync();
            output.AppendText(await stdout); var errors = await stderr; if (errors.Length > 0) output.AppendText(errors);
            output.AppendText($"\r\n[Terminé avec le code {process.ExitCode}]"); status.Text = $"Terminé (code {process.ExitCode})";
        }
        catch (Exception ex) { output.AppendText($"Erreur : {ex.Message}\r\nVérifiez que l’outil est installé et accessible dans PATH."); status.Text = "Échec de l’exécution"; }
    }
    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
    private static async Task<(string, string)> CSharpCommand(string file)
    {
        var dir = Path.GetDirectoryName(file)!; var project = Directory.GetFiles(dir, "*.csproj").FirstOrDefault();
        if (project is not null) return ("dotnet", $"run --project {Quote(project)}");
        var temp = Path.Combine(Path.GetTempPath(), "KeyceCodeRun", Path.GetFileNameWithoutExtension(file)); Directory.CreateDirectory(temp);
        await File.WriteAllTextAsync(Path.Combine(temp, "Runner.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>");
        File.Copy(file, Path.Combine(temp, "Program.cs"), true); return ("dotnet", $"run --project {Quote(Path.Combine(temp, "Runner.csproj"))}");
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F5)
        {
            if (debuggerService.IsRunning) _ = ContinueDebuggingAsync();
            else _ = RunCurrent();
            return true;
        }
        if (keyData == (Keys.Control | Keys.Oemplus) || keyData == (Keys.Control | Keys.Add)) { AdjustEditorFontSize(1); return true; }
        if (keyData == (Keys.Control | Keys.OemMinus) || keyData == (Keys.Control | Keys.Subtract)) { AdjustEditorFontSize(-1); return true; }
        if (keyData == (Keys.Control | Keys.D0) || keyData == (Keys.Control | Keys.NumPad0))
        {
            settings.EditorFontSize = 13;
            settingsService.Save(settings);
            foreach (var surface in tabs.TabPages.Cast<TabPage>().SelectMany(page => page.Controls.OfType<CodeEditorSurface>()))
                ApplyEditorSettings(surface);
            status.Text = "Taille du texte réinitialisée : 13 pt";
            return true;
        }
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

    private sealed class LightColors : ProfessionalColorTable
    {
        public override Color MenuBorder => Color.FromArgb(190, 196, 205);
        public override Color MenuItemSelected => Color.FromArgb(214, 228, 244);
        public override Color MenuItemBorder => Color.FromArgb(170, 182, 198);
        public override Color ToolStripDropDownBackground => Color.FromArgb(248, 249, 251);
        public override Color ImageMarginGradientBegin => Color.FromArgb(232, 235, 240);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(232, 235, 240);
        public override Color ImageMarginGradientEnd => Color.FromArgb(232, 235, 240);
        public override Color ToolStripGradientBegin => Color.FromArgb(232, 235, 240);
        public override Color ToolStripGradientMiddle => Color.FromArgb(232, 235, 240);
        public override Color ToolStripGradientEnd => Color.FromArgb(232, 235, 240);
    }
}
