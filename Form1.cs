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
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill, Appearance = TabAppearance.Normal, DrawMode = TabDrawMode.OwnerDrawFixed };
    private readonly ExplorerView explorer = new(new ProjectViewModel(new FileService(), new ProjectService()));
    private readonly SourceControlView sourceControl = new();
    private readonly FileService fileService = new();
    private readonly SettingsService settingsService = new();
    private readonly System.Windows.Forms.Timer autoSaveTimer = new();
    private readonly ISecretStore secretStore = new WindowsCredentialStore();
    private AiRouterService aiRouter = new([]);
    private AppSettings settings = new();
    private readonly CodeAnalysisService codeAnalysisService = new();
    private readonly LearningHintService learningHintService = new();
    private readonly FormatService formatService = new();
    private readonly CodeSymbolService codeSymbolService = new();
    private readonly CodeRenameService codeRenameService = new();
    private readonly CodeReferenceService codeReferenceService = new();
    private readonly SearchService searchService = new();
    private readonly TextSearchService textSearchService = new();
    private readonly AiAgentService aiAgentService;
    private readonly LanguageRunnerService languageRunnerService = new();
    private readonly PreviewService previewService = new();
    private readonly ScriptRunnerService scriptRunnerService = new();
    private readonly TestService testService = new();
    private readonly DebuggerService debuggerService = new();
    private readonly ExtensionService extensionService = new();
    private readonly GitHubAccountService githubAccountService = new();
    private readonly LiveDiagnosticsCoordinator liveDiagnosticsCoordinator = new();
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
    private TabControl? sideTabs;
    private MenuStrip? menuStrip;
    private ToolStripMenuItem? extensionsMenu;
    private ToolStripMenuItem? recentProjectsMenu;
    private ToolStripMenuItem? aiMenu;
    private ToolStrip? toolBar;
    private Image? officialLogo;
    private ContextMenuStrip? tabContextMenu;
    private int contextTabIndex = -1;
    private StatusStrip? statusStrip;
    private readonly RichTextBox output = new() { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.FromArgb(20, 22, 27), ForeColor = Color.Gainsboro, BorderStyle = BorderStyle.None, Font = new Font("Cascadia Mono", 10) };
    private Label? outputHeader;
    private readonly ToolStripStatusLabel status = new("Prêt") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel cursorStatus = new("Ln 1, Col 1") { TextAlign = ContentAlignment.MiddleRight };
    private string? workspace;

    public Form1()
    {
        settings = settingsService.Load();
        aiRouter = AiProviderFactory.Create(settings, secretStore);
        aiAgentService = new AiAgentService(router: aiRouter);
        aiChatViewModel = new AiChatViewModel(settings, router: aiRouter);
        autoSaveTimer.Tick += (_, _) => AutoSaveModifiedFiles();
        ConfigureAutoSaveTimer();
        Text = "KEYCE CODE"; Width = 1280; Height = 800; MinimumSize = new Size(900, 600);
        BackColor = Color.FromArgb(37, 40, 48); ForeColor = Color.Gainsboro; StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
        RestoreSession();
        if (settingsService.LastLoadWarning is { } warning)
            status.Text = warning;
        output.MouseDoubleClick += Output_MouseDoubleClick;
        outputViewModel.DiagnosticActivated += OpenDiagnostic;
        debuggerService.OutputReceived += DebuggerOutputReceived;
        debuggerService.SnapshotUpdated += DebuggerSnapshotUpdated;
        FormClosing += OnFormClosing;
        scriptRunnerService.OutputReceived += ScriptOutputReceived;
        FormClosed += async (_, _) => { autoSaveTimer.Stop(); autoSaveTimer.Dispose(); liveAnalysisCancellation?.Cancel(); liveDiagnosticsCoordinator.Dispose(); outputViewModel.Stop(); terminalViewModel.Stop(); scriptRunnerService.Stop(); await debuggerService.DisposeAsync(); officialLogo?.Dispose(); };
    }

    private void BuildLayout()
    {
        var menu = new MenuStrip { BackColor = Color.FromArgb(30, 33, 40), ForeColor = Color.Gainsboro, Font = new Font("Segoe UI", 9F), Padding = new Padding(6, 3, 6, 3), Renderer = new ToolStripProfessionalRenderer(new DarkColors()) };
        menuStrip = menu;
        var fileMenu = new ToolStripMenuItem("Fichier");
        Add(fileMenu, "Nouveau", (_, _) => NewFile(), Keys.Control | Keys.N);
        Add(fileMenu, "Nouveau projet C#…", (_, _) => CreateProject());
        Add(fileMenu, "Ouvrir…", (_, _) => OpenFile(), Keys.Control | Keys.O);
        Add(fileMenu, "Ouvrir un projet C#…", (_, _) => OpenProject());
        Add(fileMenu, "Ouvrir un dossier de projet…", (_, _) => OpenWorkspace());
        recentProjectsMenu = new ToolStripMenuItem("Projets récents");
        fileMenu.DropDownItems.Add(recentProjectsMenu);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        Add(fileMenu, "Enregistrer", (_, _) => SaveCurrent(), Keys.Control | Keys.S);
        Add(fileMenu, "Enregistrer tout", (_, _) => SaveAllOpenFiles(), Keys.Control | Keys.Alt | Keys.S);
        Add(fileMenu, "Enregistrer sous…", (_, _) => SaveAs(), Keys.Control | Keys.Shift | Keys.S);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        Add(fileMenu, "Fermer l’onglet", (_, _) => CloseCurrent(), Keys.Control | Keys.W);
        Add(fileMenu, "Fermer tous les onglets", (_, _) => CloseAll(), Keys.Control | Keys.Shift | Keys.W);
        Add(fileMenu, "Quitter", (_, _) => Close());
        var editMenu = new ToolStripMenuItem("Édition");
        Add(editMenu, "Annuler", (_, _) => Editor()?.Undo(), Keys.Control | Keys.Z);
        Add(editMenu, "Rétablir", (_, _) => Editor()?.Redo(), Keys.Control | Keys.Y);
        Add(editMenu, "Couper", (_, _) => Editor()?.Cut(), Keys.Control | Keys.X);
        Add(editMenu, "Copier", (_, _) => Editor()?.Copy(), Keys.Control | Keys.C);
        Add(editMenu, "Coller", (_, _) => Editor()?.Paste(), Keys.Control | Keys.V);
        Add(editMenu, "Commenter/décommenter la sélection", (_, _) => Editor()?.ToggleLineComment(), Keys.Control | Keys.OemQuestion);
        Add(editMenu, "Rechercher…", (_, _) => FindText(), Keys.Control | Keys.F);
        Add(editMenu, "Remplacer…", (_, _) => ReplaceText(), Keys.Control | Keys.H);
        Add(editMenu, "Aller à la ligne…", (_, _) => GoToLine(), Keys.Control | Keys.G);
        Add(editMenu, "Aller au symbole…", (_, _) => GoToSymbol(), Keys.Control | Keys.Shift | Keys.O);
        Add(editMenu, "Renommer le symbole…", (_, _) => RenameSymbol(), Keys.F2);
        Add(editMenu, "Trouver les références", (_, _) => FindReferences(), Keys.Shift | Keys.F12);
        Add(editMenu, "Aller à la définition", (_, _) => GoToDefinition(), Keys.F12);
        Add(editMenu, "Formater le document", (_, _) => FormatCurrent(), Keys.Control | Keys.Shift | Keys.I);
        Add(editMenu, "Rechercher dans les fichiers…", (_, _) => SearchInFiles(), Keys.Control | Keys.Shift | Keys.F);
        Add(editMenu, "Remplacer dans les fichiers…", (_, _) => ReplaceInFiles(), Keys.Control | Keys.Shift | Keys.H);
        var viewMenu = new ToolStripMenuItem("Affichage");
        Add(viewMenu, "Palette de commandes…", (_, _) => ShowCommandPalette(), Keys.Control | Keys.Shift | Keys.P);
        Add(viewMenu, "Thème sombre", (_, _) => SetTheme("Dark"));
        Add(viewMenu, "Thème clair", (_, _) => SetTheme("Light"));
        Add(viewMenu, "Ouvrir les paramètres…", (_, _) => OpenSettingsDialog());
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
        Add(runMenu, "Arrêter le fichier en cours", (_, _) => StopCurrentRun());
        Add(runMenu, "Envoyer une saisie au fichier…", (_, _) => SendScriptInput());
        // F5 est routé par ProcessCmdKey : il lance le fichier hors débogage
        // et continue l’exécution lorsqu’un débogage est déjà actif.
        Add(runMenu, "Continuer le débogage", async (_, _) => await ContinueDebuggingAsync());
        Add(runMenu, "Pas à pas principal", async (_, _) => await StepDebuggingAsync("over"), Keys.F10);
        Add(runMenu, "Entrer dans la fonction", async (_, _) => await StepDebuggingAsync("into"), Keys.F11);
        Add(runMenu, "Sortir de la fonction", async (_, _) => await StepDebuggingAsync("out"), Keys.Shift | Keys.F11);
        var gitMenu = new ToolStripMenuItem("Git");
        Add(gitMenu, "Actualiser l’état", async (_, _) => await RefreshGitStatusAsync());
        Add(gitMenu, "Afficher le diff", async (_, _) => await ShowGitDiffAsync(false));
        Add(gitMenu, "Afficher le diff indexé", async (_, _) => await ShowGitDiffAsync(true));
        Add(gitMenu, "Afficher l’historique", async (_, _) => await ShowGitHistoryAsync());
        Add(gitMenu, "Créer un commit…", async (_, _) => await CommitChangesAsync());
        Add(gitMenu, "Récupérer (fetch)", async (_, _) => await FetchGitAsync());
        Add(gitMenu, "Tirer les changements", async (_, _) => await PullGitAsync());
        Add(gitMenu, "Envoyer les changements", async (_, _) => await PushGitAsync());
        Add(gitMenu, "Mettre de côté (stash)…", async (_, _) => await StashGitAsync());
        Add(gitMenu, "Restaurer le stash", async (_, _) => await StashPopGitAsync());
        Add(gitMenu, "Fusionner une branche…", async (_, _) => await MergeGitAsync());
        Add(gitMenu, "Rebaser sur une branche…", async (_, _) => await RebaseGitAsync());
        Add(gitMenu, "Lister les branches", async (_, _) => await ListGitBranchesAsync());
        Add(gitMenu, "Créer une branche…", async (_, _) => await CreateGitBranchAsync());
        Add(gitMenu, "Changer de branche…", async (_, _) => await CheckoutGitBranchAsync());
        gitMenu.DropDownItems.Add(new ToolStripSeparator());
        Add(gitMenu, "Connecter un compte GitHub (facultatif)…", async (_, _) => await ConnectGitHubAsync());
        Add(gitMenu, "Gérer les comptes GitHub…", async (_, _) => await ManageGitHubAccountsAsync());
        var aiMenu = new ToolStripMenuItem("IA");
        this.aiMenu = aiMenu;
        Add(aiMenu, "Configurer Gemini/Groq…", (_, _) => ConfigureAi());
        Add(aiMenu, "Configurer Ollama…", (_, _) => ConfigureOllama());
        Add(aiMenu, "Agent : proposer une modification…", async (_, _) => await GenerateAgentPlanAsync());
        Add(aiMenu, "Agent : annuler la dernière modification", async (_, _) => await RestoreAgentPlanAsync());
        var extensionMenu = new ToolStripMenuItem("Extensions");
        extensionsMenu = extensionMenu;
        Add(extensionMenu, "Actualiser les extensions", (_, _) => RefreshExtensions());
        Add(extensionMenu, "Ouvrir le dossier des extensions", (_, _) => OpenExtensionsFolder());
        var learningMenu = new ToolStripMenuItem("Pédagogie");
        Add(learningMenu, "Activer/désactiver le mode apprentissage", (_, _) => ToggleLearningMode());
        Add(learningMenu, "Activer/désactiver le mode examen", (_, _) => ToggleExamMode());
        var helpMenu = new ToolStripMenuItem("Aide");
        Add(helpMenu, "Raccourcis clavier", (_, _) => ShowShortcuts());
        Add(helpMenu, "À propos de KEYCE CODE", (_, _) => ShowAbout());
        menu.Items.AddRange([fileMenu, editMenu, viewMenu, runMenu, gitMenu, aiMenu, extensionMenu, learningMenu, helpMenu]); MainMenuStrip = menu;

        var bar = new ToolStrip { BackColor = Color.FromArgb(30, 33, 40), ForeColor = Color.Gainsboro, GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(6, 3, 6, 3), Height = 34, Renderer = new ToolStripProfessionalRenderer(new DarkColors()) };
        toolBar = bar;
        officialLogo = LoadOfficialLogo();
        if (officialLogo is not null)
        {
            bar.Items.Add(new ToolStripLabel { Image = officialLogo, ImageScaling = ToolStripItemImageScaling.None, ToolTipText = "KEYCE CODE" });
            bar.Items.Add(new ToolStripSeparator());
        }
        Button(bar, "＋ Nouveau", () => NewFile()); Button(bar, "Ouvrir", OpenFile); Button(bar, "Projet C#", OpenProject); Button(bar, "Dossier", OpenWorkspace); Button(bar, "Enregistrer", SaveCurrent);
        bar.Items.Add(new ToolStripSeparator()); Button(bar, "▶ Exécuter (F5)", async () => await RunCurrent());
        Button(bar, "🔨 Compiler", CompileProject); Button(bar, "▶ Projet", RunProject);
        Button(bar, "🧪 Tests", async () => await RunTestsAsync()); Button(bar, "🔍 Analyser", AnalyzeCurrent);

        var split = new SplitContainer { Size = new Size(1200, 700), Dock = DockStyle.Fill, SplitterDistance = 260, BackColor = Color.FromArgb(37, 40, 48), Panel1MinSize = 180, Panel2MinSize = 400 };
        sideTabs = new TabControl { Dock = DockStyle.Fill, Appearance = TabAppearance.Normal, BackColor = Color.FromArgb(30, 33, 40), ForeColor = Color.Gainsboro };
        sideTabs.TabPages.Add(new TabPage("Explorateur") { Controls = { explorer } });
        sideTabs.TabPages.Add(new TabPage("Contrôle source") { Controls = { sourceControl } });
        split.Panel1.Controls.Add(sideTabs);
        sourceControl.RefreshRequested += async () => await RefreshGitStatusAsync();
        sourceControl.StageRequested += async path => await StageGitPathAsync(path);
        sourceControl.UnstageRequested += async path => await UnstageGitPathAsync(path);
        sourceControl.DiffRequested += async (path, staged) => await ShowGitFileDiffAsync(path, staged);
        sourceControl.CommitRequested += async () => await CommitStagedChangesAsync();
        explorer.FileOpenRequested += OpenPath;
        explorer.ProjectOpened += project =>
        {
            workspace = project.RootPath;
            SetProjectContext(project.RootPath);
            RememberRecentProject(project.EntryFilePath ?? project.RootPath);
            status.Text = $"Projet : {project.Name}";
        };
        explorer.ItemRenamed += UpdateOpenPaths;
        explorer.BeforeDelete += ConfirmOpenFilesBeforeDelete;
        // Le panneau inférieur contient plusieurs vues (console, terminal, IA) dont
        // les barres d'actions et les champs de saisie doivent rester entièrement
        // visibles, même lorsque la fenêtre n'est pas maximisée.
        var editorOutput = new SplitContainer { Size = new Size(900, 700), Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 520, BackColor = Color.FromArgb(37, 40, 48), Panel1MinSize = 180, Panel2MinSize = 220 };
        ConfigureTabInteractions();
        editorOutput.Panel1.Controls.Add(tabs);
        var outputPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 22, 27) };
        outputPanel.Controls.Add(output);
        outputHeader = new Label { Text = "  SORTIE", Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Silver, BackColor = Color.FromArgb(30, 33, 40), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        outputPanel.Controls.Add(outputHeader);
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
        RefreshRecentProjectsMenu();
        RefreshExtensions();
        ApplyPedagogicalMode();
    }

    private static Image? LoadOfficialLogo()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "keyce-code-official.jpeg");
        if (!File.Exists(path)) return null;

        try
        {
            using var source = Image.FromFile(path);
            return new Bitmap(source, new Size(26, 26));
        }
        catch
        {
            return null;
        }
    }

    private static Image? LoadOfficialLogoLarge()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "keyce-code-official.jpeg");
        if (!File.Exists(path)) return null;

        try
        {
            using var source = Image.FromFile(path);
            return new Bitmap(source);
        }
        catch
        {
            return null;
        }
    }

    private void RestoreSession()
    {
        try
        {
            if (settings.LastWorkspace is { } root && Directory.Exists(root))
                explorer.OpenProject(root);

            foreach (var path in settings.OpenFiles.Where(File.Exists))
                OpenPath(path);
            if (tabs.TabCount > 1 || tabs.SelectedTab?.Tag is not null)
                status.Text = "Session précédente restaurée.";
        }
        catch (Exception ex)
        {
            status.Text = "Session partiellement restaurée : " + ex.Message;
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (tabs.TabPages.Cast<TabPage>().Any(page => !CanClose(page)))
        {
            e.Cancel = true;
            return;
        }

        try
        {
            settings.LastWorkspace = workspace is not null && Directory.Exists(workspace) ? workspace : null;
            settings.OpenFiles = tabs.TabPages.Cast<TabPage>()
                .Select(PathOf)
                .Where(path => path is not null && File.Exists(path))
                .Select(path => Path.GetFullPath(path!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList();
            settingsService.Save(settings);
        }
        catch (Exception ex)
        {
            status.Text = "Impossible de sauvegarder la session : " + ex.Message;
        }
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
            sourceControl.ClearRepository();
            _ = RefreshGitDecorationsAsync();
        }
    }

    private async Task RefreshGitDecorationsAsync()
    {
        if (gitService is null) return;
        try
        {
            var snapshot = await gitService.GetStatusAsync();
            explorer.ApplyGitStatus(snapshot);
            sourceControl.ApplySnapshot(snapshot);
        }
        catch
        {
            sourceControl.ClearRepository();
            /* Un dossier peut ne pas être un dépôt Git. */
        }
    }

    private async Task RefreshGitStatusAsync()
    {
        if (gitService is null) { status.Text = "Aucun dépôt Git ouvert."; return; }
        try
        {
            var snapshot = await gitService.GetStatusAsync();
            explorer.ApplyGitStatus(snapshot);
            sourceControl.ApplySnapshot(snapshot);
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

    private async Task StageGitPathAsync(string path)
    {
        if (gitService is null) { status.Text = "Aucun dépôt Git ouvert."; return; }
        try { await gitService.StageAsync([path]); await RefreshGitStatusAsync(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git — indexer", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task UnstageGitPathAsync(string path)
    {
        if (gitService is null) { status.Text = "Aucun dépôt Git ouvert."; return; }
        try { await gitService.UnstageAsync([path]); await RefreshGitStatusAsync(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git — désindexer", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task ShowGitFileDiffAsync(string path, bool staged)
    {
        if (gitService is null) { status.Text = "Aucun dépôt Git ouvert."; return; }
        try
        {
            var diff = await gitService.GetDiffAsync(path, staged);
            SelectBottomTab("Sortie");
            output.Text = string.IsNullOrWhiteSpace(diff) ? "Aucun changement à afficher.\r\n" : diff;
            status.Text = staged ? $"Git : diff indexé de {path}." : $"Git : diff de {path}.";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git — diff", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task CommitStagedChangesAsync()
    {
        if (gitService is null) { status.Text = "Aucun dépôt Git ouvert."; return; }
        var message = Microsoft.VisualBasic.Interaction.InputBox("Message du commit (changements indexés uniquement) :", "Git — créer un commit");
        if (string.IsNullOrWhiteSpace(message)) return;
        try
        {
            await gitService.CommitAsync(message);
            await RefreshGitStatusAsync();
            status.Text = "Git : commit créé à partir des changements indexés.";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git — commit", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task ShowGitDiffAsync(bool staged)
    {
        if (gitService is null) { status.Text = "Aucun dépôt Git ouvert."; return; }
        try
        {
            var diff = await gitService.GetDiffAsync(staged: staged);
            SelectBottomTab("Sortie");
            output.Text = string.IsNullOrWhiteSpace(diff) ? "Aucun changement à afficher.\r\n" : diff;
            status.Text = staged ? "Git : diff indexé affiché." : "Git : diff affiché.";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task ShowGitHistoryAsync()
    {
        if (gitService is null) { status.Text = "Aucun dépôt Git ouvert."; return; }
        try
        {
            var history = await gitService.GetHistoryAsync();
            SelectBottomTab("Sortie");
            output.Text = history.Count == 0
                ? "Aucun commit dans ce dépôt.\r\n"
                : string.Join(Environment.NewLine, history.Select(commit =>
                    $"{commit.Hash[..Math.Min(8, commit.Hash.Length)]}  {commit.Date:yyyy-MM-dd HH:mm}  {commit.Author} — {commit.Subject}")) + Environment.NewLine;
            status.Text = $"Git : {history.Count} commit(s) affiché(s).";
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

    private async Task ConnectGitHubAsync()
    {
        var consent = MessageBox.Show(this,
            "La connexion est facultative. KEYCE CODE va ouvrir l’authentification officielle de Git Credential Manager.\r\n\r\nAucun mot de passe ni jeton ne sera lu ou enregistré par KEYCE CODE.",
            "Connecter GitHub", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
        if (consent != DialogResult.OK) return;
        try
        {
            status.Text = "GitHub : authentification en cours…";
            await githubAccountService.LoginAsync();
            var accounts = await githubAccountService.ListAccountsAsync();
            status.Text = accounts.Count == 0 ? "GitHub : aucun compte connecté." : $"GitHub : {accounts.Count} compte(s) connecté(s).";
            MessageBox.Show(this, accounts.Count == 0 ? "La connexion GitHub n’a pas été détectée." : $"Compte(s) disponible(s) : {string.Join(", ", accounts.Select(account => account.Login))}", "GitHub", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "GitHub", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task ManageGitHubAccountsAsync()
    {
        try
        {
            var accounts = await githubAccountService.ListAccountsAsync();
            if (accounts.Count == 0)
            {
                var connect = MessageBox.Show(this, "Aucun compte GitHub n’est connecté. Voulez-vous en connecter un ?", "GitHub", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (connect == DialogResult.Yes) await ConnectGitHubAsync();
                return;
            }
            var list = string.Join(Environment.NewLine, accounts.Select(account => "• " + account.Login));
            var logout = MessageBox.Show(this, $"Comptes GitHub détectés :\r\n\r\n{list}\r\n\r\nOui : déconnecter un compte\r\nNon : conserver les comptes", "Comptes GitHub", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (logout != DialogResult.Yes) return;
            var login = Microsoft.VisualBasic.Interaction.InputBox("Nom du compte à déconnecter :", "GitHub — déconnexion", accounts[0].Login);
            if (string.IsNullOrWhiteSpace(login)) return;
            await githubAccountService.LogoutAsync(login);
            status.Text = $"GitHub : {login.Trim()} déconnecté.";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "GitHub", MessageBoxButtons.OK, MessageBoxIcon.Error); }
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

    private async Task FetchGitAsync()
    {
        if (gitService is null) { status.Text = "Aucun dépôt Git ouvert."; return; }
        try { await gitService.FetchAsync(); await RefreshGitStatusAsync(); status.Text = "Git : fetch terminé."; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git — fetch", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task StashGitAsync()
    {
        if (gitService is null) { status.Text = "Aucun dépôt Git ouvert."; return; }
        var message = Microsoft.VisualBasic.Interaction.InputBox("Message facultatif du stash :", "Git — stash");
        try { await gitService.StashPushAsync(message); await RefreshGitStatusAsync(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git — stash", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task StashPopGitAsync()
    {
        if (gitService is null) { status.Text = "Aucun dépôt Git ouvert."; return; }
        try { await gitService.StashPopAsync(); await RefreshGitStatusAsync(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git — restaurer le stash", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task MergeGitAsync()
    {
        if (gitService is null) { status.Text = "Aucun dépôt Git ouvert."; return; }
        var branch = Microsoft.VisualBasic.Interaction.InputBox("Nom de la branche à fusionner :", "Git — merge");
        if (string.IsNullOrWhiteSpace(branch)) return;
        try { await gitService.MergeAsync(branch); await RefreshGitStatusAsync(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git — merge", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task RebaseGitAsync()
    {
        if (gitService is null) { status.Text = "Aucun dépôt Git ouvert."; return; }
        var branch = Microsoft.VisualBasic.Interaction.InputBox("Nom de la branche de base :", "Git — rebase");
        if (string.IsNullOrWhiteSpace(branch)) return;
        try { await gitService.RebaseAsync(branch); await RefreshGitStatusAsync(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git — rebase", MessageBoxButtons.OK, MessageBoxIcon.Error); }
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

    private void ShowCommandPalette()
    {
        var commands = new List<(string Label, Action Action)>
        {
            ("Fichier : Nouveau", NewFile),
            ("Fichier : Ouvrir", OpenFile),
            ("Fichier : Enregistrer", SaveCurrent),
            ("Fichier : Enregistrer tout", () => _ = SaveAllOpenFiles()),
            ("Fichier : Enregistrer sous", SaveAs),
            ("Fichier : Fermer l’onglet", CloseCurrent),
            ("Fichier : Fermer tous les onglets", CloseAll),
            ("Édition : Annuler", () => Editor()?.Undo()),
            ("Édition : Rétablir", () => Editor()?.Redo()),
            ("Édition : Rechercher", FindText),
            ("Édition : Remplacer", ReplaceText),
            ("Édition : Aller à la ligne", GoToLine),
            ("Édition : Aller au symbole", GoToSymbol),
            ("Édition : Renommer le symbole", RenameSymbol),
            ("Édition : Trouver les références", FindReferences),
            ("Édition : Aller à la définition", GoToDefinition),
            ("Édition : Formater le document", FormatCurrent),
            ("Projet : Compiler", CompileProject),
            ("Projet : Exécuter", RunProject),
            ("Projet : Lancer les tests", async () => await RunTestsAsync()),
            ("Projet : Analyser le code", AnalyzeCurrent),
            ("Débogage : Ajouter/retirer un point d’arrêt", ToggleBreakpoint),
            ("Débogage : Démarrer", async () => await StartDebuggingAsync()),
            ("Débogage : Arrêter", async () => await StopDebuggingAsync()),
            ("Git : Actualiser", async () => await RefreshGitStatusAsync()),
            ("IA : Ouvrir l’assistant", () => SelectBottomTab("Assistant IA")),
            ("Extensions : Actualiser", RefreshExtensions)
        };
        if (settings.ExamMode)
            commands.RemoveAll(command => command.Label.StartsWith("IA :", StringComparison.Ordinal) || command.Label.StartsWith("Extensions :", StringComparison.Ordinal));
        else
            commands.AddRange(extensionService.Commands.Select(command => ($"Extension : {command.Label}", command.Action)));
        using var palette = new CommandPaletteForm(commands);
        palette.ShowDialog(this);
    }
    private ICodeEditorSurface? Editor() => EditorOf(tabs.SelectedTab);
    private static ICodeEditorSurface? EditorOf(TabPage? page) => page?.Controls.OfType<ICodeEditorSurface>().FirstOrDefault();
    private string? PathOf(TabPage? page = null) => (page ?? tabs.SelectedTab)?.Tag as string;

    private void ConfigureTabInteractions()
    {
        tabs.DrawItem += DrawEditorTab;
        tabs.MouseDown += EditorTabs_MouseDown;

        tabContextMenu = new ContextMenuStrip
        {
            Renderer = new ToolStripProfessionalRenderer(new DarkColors())
        };
        AddTabContextCommand("Fermer", CloseContextTab);
        AddTabContextCommand("Fermer les autres", () => CloseOtherTabs(contextTabIndex));
        AddTabContextCommand("Fermer à droite", () => CloseTabsToRight(contextTabIndex));
        tabContextMenu.Items.Add(new ToolStripSeparator());
        AddTabContextCommand("Fermer tous les onglets", CloseAll);
        tabs.ContextMenuStrip = tabContextMenu;
    }

    private void AddTabContextCommand(string text, Action action)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (_, _) => action();
        tabContextMenu!.Items.Add(item);
    }

    private void EditorTabs_MouseDown(object? sender, MouseEventArgs e)
    {
        var index = TabIndexAt(e.Location);
        if (index < 0) return;

        if (e.Button == MouseButtons.Right)
        {
            tabs.SelectedIndex = index;
            contextTabIndex = index;
            tabContextMenu?.Show(tabs, e.Location);
            return;
        }

        if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && CloseGlyphRect(index).Contains(e.Location)))
        {
            ClosePageAt(index);
            return;
        }

        if (e.Button == MouseButtons.Left)
            tabs.SelectedIndex = index;
    }

    private int TabIndexAt(Point location)
    {
        for (var index = 0; index < tabs.TabCount; index++)
            if (tabs.GetTabRect(index).Contains(location)) return index;
        return -1;
    }

    private Rectangle CloseGlyphRect(int index)
    {
        var tab = tabs.GetTabRect(index);
        return new Rectangle(tab.Right - 23, tab.Top + 5, 18, Math.Max(16, tab.Height - 10));
    }

    private void DrawEditorTab(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= tabs.TabPages.Count) return;
        var page = tabs.TabPages[e.Index];
        var selected = e.Index == tabs.SelectedIndex;
        var background = selected ? Color.FromArgb(45, 49, 58) : Color.FromArgb(30, 33, 40);
        using var backgroundBrush = new SolidBrush(background);
        e.Graphics.FillRectangle(backgroundBrush, e.Bounds);
        using var borderPen = new Pen(selected ? Color.FromArgb(0, 160, 220) : Color.FromArgb(60, 64, 72));
        e.Graphics.DrawRectangle(borderPen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);

        var textBounds = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 34), e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, page.Text, Font, textBounds, Color.Gainsboro,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        var close = CloseGlyphRect(e.Index);
        TextRenderer.DrawText(e.Graphics, "×", Font, close, Color.Silver,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

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
            var surface = new CodeEditorSurface(); page.Controls.Add(surface); AttachEditor(surface); ApplyEditorSettings(surface); surface.ApplyLanguage(Path.GetExtension(path)); surface.Text = fileService.ReadText(path); surface.IsReadOnly = fileService.IsReadOnly(path); surface.Modified = false;
            tabs.TabPages.Add(page); tabs.SelectedTab = page; UpdateTitle(page); SetProjectContext(workspace ?? path); status.Text = surface.IsReadOnly ? $"Lecture seule : {path}" : path;
        }
        catch (Exception ex) { MessageBox.Show(this, $"Impossible d’ouvrir le fichier.\n{ex.Message}", "KeyceCode", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    private void OpenProject() => explorer.OpenProjectDialog(this);

    private void CreateProject()
    {
        using var folder = new FolderBrowserDialog { Description = "Choisissez où créer le projet C#" };
        if (folder.ShowDialog(this) != DialogResult.OK) return;
        var name = Microsoft.VisualBasic.Interaction.InputBox("Nom du projet C# :", "Nouveau projet", "MonProjet").Trim();
        if (name.Length == 0) return;
        try
        {
            var projectPath = new ProjectService().CreateCSharpProject(folder.SelectedPath, name);
            explorer.OpenProject(projectPath);
            status.Text = $"Projet créé : {name}";
        }
        catch (Exception ex) { MessageBox.Show(this, $"Impossible de créer le projet.\n{ex.Message}", "KEYCE CODE", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void OpenRecentProject(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            settings.RecentProjects.RemoveAll(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase));
            settingsService.Save(settings);
            RefreshRecentProjectsMenu();
            status.Text = "Le projet récent n’existe plus.";
            return;
        }
        try { explorer.OpenProject(path); }
        catch (Exception ex) { MessageBox.Show(this, $"Impossible d’ouvrir le projet récent.\n{ex.Message}", "KEYCE CODE", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void RememberRecentProject(string path)
    {
        var fullPath = Path.GetFullPath(path);
        settings.RecentProjects.RemoveAll(item => string.Equals(item, fullPath, StringComparison.OrdinalIgnoreCase));
        settings.RecentProjects.Insert(0, fullPath);
        if (settings.RecentProjects.Count > 10) settings.RecentProjects.RemoveRange(10, settings.RecentProjects.Count - 10);
        settingsService.Save(settings);
        RefreshRecentProjectsMenu();
    }

    private void RefreshRecentProjectsMenu()
    {
        if (recentProjectsMenu is null) return;
        recentProjectsMenu.DropDownItems.Clear();
        var valid = settings.RecentProjects.Where(path => File.Exists(path) || Directory.Exists(path)).ToList();
        if (valid.Count == 0)
        {
            recentProjectsMenu.DropDownItems.Add(new ToolStripMenuItem("Aucun projet récent") { Enabled = false });
        }
        else
        {
            foreach (var path in valid)
            {
                var item = new ToolStripMenuItem($"{Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))} — {path}");
                item.ToolTipText = path;
                item.Click += (_, _) => OpenRecentProject(path);
                recentProjectsMenu.DropDownItems.Add(item);
            }
        }
        recentProjectsMenu.DropDownItems.Add(new ToolStripSeparator());
        var clear = new ToolStripMenuItem("Effacer l’historique");
        clear.Click += (_, _) => { settings.RecentProjects.Clear(); settingsService.Save(settings); RefreshRecentProjectsMenu(); };
        recentProjectsMenu.DropDownItems.Add(clear);
    }

    private void OpenWorkspace()
    {
        using var dialog = new FolderBrowserDialog { Description = "Choisissez le dossier contenant un projet C#" };
        if (dialog.ShowDialog(this) == DialogResult.OK) explorer.OpenProject(dialog.SelectedPath);
    }

    private void SaveCurrent() { if (tabs.SelectedTab is { } page) SavePage(page); }

    private void ApplyEditorSettings(ICodeEditorSurface surface)
    {
        surface.ApplyAppearance(settings);
        surface.SetExamMode(settings.ExamMode);
    }

    private void AttachEditor(ICodeEditorSurface surface)
    {
        surface.CaretPositionChanged += (_, _) => UpdateCursorStatus(surface);
    }

    private void UpdateCursorStatus(ICodeEditorSurface? surface)
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
        foreach (var surface in tabs.TabPages.Cast<TabPage>().SelectMany(page => page.Controls.OfType<ICodeEditorSurface>()))
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
            ApplyToolStripColors(menuStrip, chrome, text);
        }
        if (toolBar is not null)
        {
            toolBar.BackColor = chrome;
            toolBar.ForeColor = text;
            toolBar.Renderer = new ToolStripProfessionalRenderer(dark ? new DarkColors() : new LightColors());
            ApplyToolStripColors(toolBar, chrome, text);
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
        if (outputHeader is not null)
        {
            outputHeader.BackColor = dark ? Color.FromArgb(30, 33, 40) : Color.FromArgb(232, 235, 240);
            outputHeader.ForeColor = dark ? Color.Silver : Color.FromArgb(32, 35, 40);
        }
    }

    private void ToggleLearningMode()
    {
        settings.LearningMode = !settings.LearningMode;
        if (settings.LearningMode) settings.ExamMode = false;
        settingsService.Save(settings);
        ApplyPedagogicalMode();
        status.Text = settings.LearningMode ? "Mode apprentissage activé." : "Mode apprentissage désactivé.";
    }

    private void ToggleExamMode()
    {
        settings.ExamMode = !settings.ExamMode;
        if (settings.ExamMode) settings.LearningMode = false;
        settingsService.Save(settings);
        ApplyPedagogicalMode();
        status.Text = settings.ExamMode ? "Mode examen activé : IA et presse-papiers désactivés." : "Mode examen désactivé.";
    }

    private void ApplyPedagogicalMode()
    {
        outputViewModel.LearningMode = settings.LearningMode;
        foreach (var surface in tabs.TabPages.Cast<TabPage>().SelectMany(page => page.Controls.OfType<ICodeEditorSurface>()))
            surface.SetExamMode(settings.ExamMode);
        if (aiChatView is not null)
        {
            aiChatView.SetExamMode(settings.ExamMode);
            aiChatView.IsEnabled = !settings.ExamMode;
        }
        if (aiMenu is not null)
            foreach (ToolStripItem item in aiMenu.DropDownItems) item.Enabled = !settings.ExamMode;
        if (extensionsMenu is not null)
            extensionsMenu.Enabled = !settings.ExamMode;
        var terminalPage = bottomTabs?.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => string.Equals(page.Text, "Terminal", StringComparison.Ordinal));
        if (terminalPage is not null)
        {
            if (settings.ExamMode) terminalViewModel.Stop();
            terminalPage.Enabled = !settings.ExamMode;
            if (settings.ExamMode && bottomTabs?.SelectedTab == terminalPage)
                SelectBottomTab("Sortie");
        }
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

    private static void ApplyToolStripColors(ToolStrip strip, Color background, Color text)
    {
        foreach (ToolStripItem item in strip.Items)
        {
            item.ForeColor = text;
            item.BackColor = background;
            if (item is ToolStripDropDownItem dropDown)
                ApplyToolStripColors(dropDown.DropDown, background, text);
        }
    }

    private void AdjustEditorFontSize(double amount)
    {
        settings.EditorFontSize = Math.Clamp(settings.EditorFontSize + amount, 8, 32);
        settingsService.Save(settings);
        foreach (var surface in tabs.TabPages.Cast<TabPage>().SelectMany(page => page.Controls.OfType<ICodeEditorSurface>()))
            ApplyEditorSettings(surface);
        status.Text = $"Taille du texte : {settings.EditorFontSize:0} pt";
    }

    private void ToggleWordWrap()
    {
        settings.WordWrap = !settings.WordWrap;
        settingsService.Save(settings);
        foreach (var surface in tabs.TabPages.Cast<TabPage>().SelectMany(page => page.Controls.OfType<ICodeEditorSurface>()))
            ApplyEditorSettings(surface);
        status.Text = settings.WordWrap ? "Retour à la ligne activé." : "Retour à la ligne désactivé.";
    }

    private void SetTabSize(int size)
    {
        settings.TabSize = size;
        settingsService.Save(settings);
        foreach (var surface in tabs.TabPages.Cast<TabPage>().SelectMany(page => page.Controls.OfType<ICodeEditorSurface>()))
            ApplyEditorSettings(surface);
        status.Text = $"Tabulations : {size} espaces.";
    }

    private void ShowShortcuts()
    {
        MessageBox.Show(this,
            "Ctrl+N  Nouveau fichier\nCtrl+O  Ouvrir\nCtrl+S  Enregistrer\nCtrl+Maj+S  Enregistrer sous\nCtrl+W  Fermer l’onglet\nCtrl+Z  Annuler\nCtrl+Y  Rétablir\nCtrl+F  Rechercher\nCtrl+H  Remplacer\nCtrl+G  Aller à la ligne\nCtrl+Maj+O  Aller au symbole\nF2  Renommer le symbole\nF12  Aller à la définition\nCtrl+Maj+I  Formater le document\nCtrl+Maj+F  Rechercher dans les fichiers\nCtrl+Maj+H  Remplacer dans les fichiers\nCtrl+Espace  Auto-complétion C#\nCtrl+Maj+P  Palette de commandes\nCtrl++ / Ctrl+-  Zoomer\nCtrl+0  Taille normale\nF5  Exécuter ou continuer le débogage\nMaj+F5  Démarrer le débogage\nF9  Point d’arrêt\nF10  Pas à pas principal\nF11  Entrer dans la fonction\nMaj+F11  Sortir de la fonction",
            "Raccourcis clavier — KEYCE CODE",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ShowAbout()
    {
        using var dialog = new Form
        {
            Text = "À propos de KEYCE CODE",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(440, 280),
            BackColor = Color.FromArgb(30, 33, 40),
            ForeColor = Color.Gainsboro
        };
        var picture = new PictureBox { Dock = DockStyle.Top, Height = 170, SizeMode = PictureBoxSizeMode.Zoom, Image = LoadOfficialLogoLarge() };
        var text = new Label
        {
            Dock = DockStyle.Fill,
            Text = "KEYCE CODE\nÉditeur de code Windows\n\nÉdition, analyse, compilation, terminal et outils de développement intégrés.",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.Gainsboro,
            Padding = new Padding(12)
        };
        dialog.Controls.Add(text);
        dialog.Controls.Add(picture);
        dialog.ShowDialog(this);
        picture.Image?.Dispose();
    }

    private void ConfigureOllama()
    {
        OpenSettingsDialog();
    }

    private void ConfigureAi()
    {
        using var dialog = new AiSettingsDialog(settings, secretStore);
        dialog.ApplyTheme(!string.Equals(settings.Theme, "Light", StringComparison.OrdinalIgnoreCase));
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        dialog.SaveSecrets();
        var models = dialog.GetModels(); settings.GeminiModel = models.GeminiModel; settings.GroqModel = models.GroqModel;
        settingsService.Save(settings); aiRouter = AiProviderFactory.Create(settings, secretStore);
        aiChatViewModel = new AiChatViewModel(settings, router: aiRouter);
        if (aiChatView is not null) aiChatView.DataContext = aiChatViewModel;
        status.Text = aiRouter.ProviderNames.Count == 0 ? "IA : aucun relais configuré." : "Configuration IA enregistrée.";
    }

    private void OpenSettingsDialog()
    {
        using var dialog = new SettingsDialog(settings);
        dialog.ApplyTheme(!string.Equals(settings.Theme, "Light", StringComparison.OrdinalIgnoreCase));
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        settings = dialog.GetSettings();
        settingsService.Save(settings);
        ConfigureAutoSaveTimer();
        RefreshRecentProjectsMenu();
        ApplyChromeTheme();
        foreach (var surface in tabs.TabPages.Cast<TabPage>().SelectMany(page => page.Controls.OfType<ICodeEditorSurface>()))
            ApplyEditorSettings(surface);
        ApplyPedagogicalMode();
        status.Text = "Paramètres enregistrés.";
    }

    private bool SaveAllOpenFiles()
    {
        foreach (var page in tabs.TabPages.Cast<TabPage>().ToList())
            if (EditorOf(page)?.Modified == true && !SavePage(page)) return false;
        return true;
    }

    private void ConfigureAutoSaveTimer()
    {
        autoSaveTimer.Stop();
        autoSaveTimer.Interval = Math.Clamp(settings.AutoSaveDelaySeconds, 10, 3600) * 1000;
        if (settings.AutoSave) autoSaveTimer.Start();
    }

    private void AutoSaveModifiedFiles()
    {
        foreach (var page in tabs.TabPages.Cast<TabPage>().ToList())
        {
            var editor = EditorOf(page);
            var path = PathOf(page);
            if (editor?.Modified != true || path is null || !File.Exists(path)) continue;
            try
            {
                fileService.WriteText(path, editor.Text);
                editor.Modified = false;
                UpdateTitle(page);
                status.Text = $"Auto-save : {Path.GetFileName(path)}";
            }
            catch (Exception ex) { status.Text = $"Auto-save impossible : {ex.Message}"; }
        }
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
            debuggerService.PreferredAdapterPath = settings.DebuggerPath;
            SaveAllOpenFiles();
            var project = outputViewModel.ResolveRunnableProject(path);
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
    private bool SavePage(TabPage page, string? requestedPath = null)
    {
        if (EditorOf(page) is not { } editor) return false;
        var path = requestedPath ?? PathOf(page);
        if (path is null)
        {
            using var dialog = new SaveFileDialog { FileName = "NouveauFichier.cs", Filter = "Tous les fichiers (*.*)|*.*" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            path = dialog.FileName;
        }
        try
        {
            if (settings.FormatOnSave && string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase))
            {
                var formatted = formatService.FormatCSharp(editor.Text, settings.TabSize);
                if (!string.Equals(formatted, editor.Text, StringComparison.Ordinal)) editor.ReplaceText(formatted);
            }
            fileService.WriteText(path, editor.Text);
            page.Tag = Path.GetFullPath(path);
            editor.Modified = false;
            UpdateTitle(page);
            status.Text = settings.FormatOnSave && string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase)
                ? $"Formaté et enregistré : {path}"
                : $"Enregistré : {path}";
            if (workspace is not null) explorer.RefreshProject();
            return true;
        }
        catch (Exception ex) { MessageBox.Show(this, $"Impossible d’enregistrer.\n{ex.Message}", "KeyceCode", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
    }
    private void UpdateTitle(TabPage page)
    {
        var editor = EditorOf(page);
        page.Text = (PathOf(page) is { } p ? Path.GetFileName(p) : "Sans titre") + (editor?.Modified == true ? " •" : "");
        Text = $"{page.Text} — KEYCE CODE";
        if (editor is not null) { editor.TextChanged -= OnEditorChanged; editor.TextChanged += OnEditorChanged; }
    }
    private void OnEditorChanged(object? sender, EventArgs e)
    {
        if (sender is not ICodeEditorSurface editor) return;
        var page = tabs.TabPages.Cast<TabPage>().FirstOrDefault(t => EditorOf(t) == editor); if (page is null) return;
        UpdateTitle(page);
        if (ReferenceEquals(page, tabs.SelectedTab) && aiChatViewModel is not null)
            aiChatViewModel.ContextText = editor.Text;
        editor.SetDiagnostics([]);
        ScheduleLiveAnalysis(editor);
    }

    private void ScheduleLiveAnalysis(ICodeEditorSurface editor)
    {
        var extension = Path.GetExtension(PathOf() ?? ".cs");
        if (!string.IsNullOrEmpty(extension) && !string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase)) return;
        liveAnalysisCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        liveAnalysisCancellation = cancellation;
        _ = AnalyzeAfterPauseAsync(editor, cancellation.Token);
    }

    private async Task AnalyzeAfterPauseAsync(ICodeEditorSurface editor, CancellationToken cancellationToken)
    {
        try
        {
            var diagnostics = await liveDiagnosticsCoordinator.AnalyzeAsync(
                editor.Text,
                source => codeAnalysisService.Analyze(source),
                TimeSpan.FromMilliseconds(650),
                cancellationToken);
            if (diagnostics is null || cancellationToken.IsCancellationRequested || !ReferenceEquals(editor, Editor())) return;
            BeginInvoke(() =>
            {
                if (!cancellationToken.IsCancellationRequested && ReferenceEquals(editor, Editor()))
                {
                    editor.SetDiagnostics(diagnostics);
                    status.Text = diagnostics.Count == 0
                        ? "Analyse en direct : aucune erreur détectée."
                        : $"Analyse en direct : {diagnostics.Count} problème(s). Utilisez Analyser pour le détail.";
                }
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
    private void CloseContextTab() => ClosePageAt(contextTabIndex);

    private void ClosePageAt(int index)
    {
        if (index < 0 || index >= tabs.TabPages.Count) return;
        var page = tabs.TabPages[index];
        if (!CanClose(page)) return;
        tabs.TabPages.Remove(page);
        page.Dispose();
        if (tabs.TabCount == 0) NewFile();
        else
        {
            tabs.SelectedIndex = Math.Min(index, tabs.TabCount - 1);
            if (tabs.SelectedTab is { } active) UpdateTitle(active);
        }
    }

    private void CloseCurrent() => ClosePageAt(tabs.SelectedIndex);

    private void CloseOtherTabs(int keepIndex)
    {
        if (keepIndex < 0 || keepIndex >= tabs.TabPages.Count) return;
        var pages = tabs.TabPages.Cast<TabPage>().Where((_, index) => index != keepIndex).ToList();
        if (!CanClosePages(pages)) return;
        foreach (var page in pages)
        {
            tabs.TabPages.Remove(page);
            page.Dispose();
        }
        tabs.SelectedIndex = Math.Min(keepIndex, tabs.TabCount - 1);
        if (tabs.SelectedTab is { } active) UpdateTitle(active);
    }

    private void CloseTabsToRight(int startIndex)
    {
        if (startIndex < 0 || startIndex >= tabs.TabPages.Count) return;
        var pages = tabs.TabPages.Cast<TabPage>().Skip(startIndex + 1).ToList();
        if (!CanClosePages(pages)) return;
        foreach (var page in pages)
        {
            tabs.TabPages.Remove(page);
            page.Dispose();
        }
        tabs.SelectedIndex = Math.Min(startIndex, tabs.TabCount - 1);
        if (tabs.SelectedTab is { } active) UpdateTitle(active);
    }

    private bool CanClosePages(IReadOnlyList<TabPage> pages)
    {
        foreach (var page in pages)
            if (!CanClose(page)) return false;
        return true;
    }

    private void CloseAll()
    {
        var pages = tabs.TabPages.Cast<TabPage>().ToList();
        // Confirmer tous les fichiers avant de retirer le moindre onglet :
        // une annulation ne doit pas produire une fermeture partielle.
        if (!CanClosePages(pages)) return;

        foreach (var page in pages)
        {
            tabs.TabPages.Remove(page);
            page.Dispose();
        }
        NewFile();
        status.Text = "Tous les onglets ont été fermés.";
    }

    private void FindText()
    {
        var editor = Editor(); if (editor is null) return;
        using var dialog = new SearchReplaceDialog(false);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Query.Length == 0) return;
        var matches = textSearchService.Find(editor.Text, dialog.Query, dialog.Options);
        var start = editor.SelectionStart + editor.SelectionLength;
        var match = matches.FirstOrDefault(item => item.Index >= start);
        if (match.Length == 0 && matches.Count > 0) match = matches[0];
        if (match.Length > 0) { editor.Select(match.Index, match.Length); editor.Focus(); }
        else status.Text = "Aucun résultat";
    }

    private void ReplaceText()
    {
        var editor = Editor();
        if (editor is null) return;
        using var dialog = new SearchReplaceDialog(true);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Query.Length == 0) return;
        var result = textSearchService.ReplaceAll(editor.Text, dialog.Query, dialog.Replacement, dialog.Options);
        if (result.Count == 0)
        {
            status.Text = "Aucun résultat à remplacer.";
            return;
        }

        editor.ReplaceText(result.Text);
        status.Text = $"{result.Count} occurrence(s) remplacée(s).";
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

    private void GoToSymbol()
    {
        var editor = Editor();
        if (editor is null) return;
        var symbols = codeSymbolService.GetSymbols(editor.Text);
        if (symbols.Count == 0) { status.Text = "Aucun symbole trouvé dans ce fichier."; return; }
        using var palette = new CommandPaletteForm(symbols.Select(symbol =>
            (symbol.Display, (Action)(() => SelectEditorPosition(symbol.Line, symbol.Column)))));
        palette.ShowDialog(this);
    }

    private void RenameSymbol()
    {
        var editor = Editor();
        if (editor is null) return;
        var newName = Microsoft.VisualBasic.Interaction.InputBox("Nouveau nom du symbole :", "Renommer le symbole");
        if (string.IsNullOrWhiteSpace(newName)) return;
        try
        {
            var result = codeRenameService.Rename(editor.Text, editor.SelectionStart, newName.Trim());
            if (result is null) { status.Text = "Le curseur n’est pas placé sur un symbole C# renommable."; return; }
            editor.ReplaceText(result.Text);
            status.Text = result.Occurrences == 0 ? "Le symbole porte déjà ce nom." : $"Symbole renommé : {result.Occurrences} occurrence(s).";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Renommer le symbole", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void FindReferences()
    {
        var editor = Editor();
        if (editor is null) return;
        var references = codeReferenceService.FindReferences(editor.Text, editor.SelectionStart);
        output.Clear();
        if (references.Count == 0)
        {
            output.AppendText("Aucune référence C# trouvée.\r\n");
            status.Text = "Aucune référence trouvée.";
            return;
        }

        var name = references[0].Name;
        output.AppendText($"Références de {name} ({references.Count}) :\r\n");
        foreach (var reference in references)
            output.AppendText($"{PathOf() ?? "fichier courant"}:{reference.Line}:{reference.Column}\r\n");
        status.Text = $"{references.Count} référence(s) trouvée(s) pour {name}.";
    }

    private void GoToDefinition()
    {
        var editor = Editor();
        if (editor is null) return;
        var offset = Math.Clamp(editor.SelectionStart, 0, editor.Text.Length);
        var start = offset;
        while (start > 0 && (char.IsLetterOrDigit(editor.Text[start - 1]) || editor.Text[start - 1] == '_')) start--;
        var end = offset;
        while (end < editor.Text.Length && (char.IsLetterOrDigit(editor.Text[end]) || editor.Text[end] == '_')) end++;
        var name = editor.Text[start..end];
        if (name.Length == 0) { status.Text = "Aucun symbole sous le curseur."; return; }
        var symbol = codeSymbolService.GetSymbols(editor.Text).FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));
        if (symbol is null) { status.Text = $"Définition introuvable pour « {name} »."; return; }
        SelectEditorPosition(symbol.Line, symbol.Column);
        status.Text = $"Définition atteinte : {symbol.Kind} {symbol.Name}.";
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
            var formatted = formatService.FormatCSharp(editor.Text, settings.TabSize);
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

    private void ReplaceInFiles()
    {
        var root = workspace ?? (PathOf() is { } file ? Path.GetDirectoryName(file) : null);
        if (string.IsNullOrWhiteSpace(root))
        {
            MessageBox.Show(this, "Ouvrez un dossier ou un fichier avant de remplacer dans les fichiers.", "Remplacement", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var query = Microsoft.VisualBasic.Interaction.InputBox("Texte à rechercher dans le projet :", "Remplacer dans les fichiers");
        if (string.IsNullOrEmpty(query)) return;
        var replacement = Microsoft.VisualBasic.Interaction.InputBox("Remplacer par :", "Remplacer dans les fichiers");
        try
        {
            var matches = searchService.Search(root, query);
            if (matches.Count == 0) { status.Text = "Aucun résultat à remplacer."; return; }
            var answer = MessageBox.Show(this, $"{matches.Count} ligne(s) correspondent. Remplacer dans les fichiers ?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;
            var results = searchService.ReplaceAll(root, query, replacement);
            output.Clear();
            foreach (var result in results) output.AppendText($"{result.FilePath} : {result.Occurrences} occurrence(s) remplacée(s).\r\n");
            SynchronizeOpenFilesAfterReplace(results);
            status.Text = $"Remplacement terminé : {results.Sum(result => result.Occurrences)} occurrence(s).";
            explorer.RefreshProject();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Erreur de remplacement", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void SynchronizeOpenFilesAfterReplace(IReadOnlyList<ReplacementResult> results)
    {
        var changedPaths = results.Select(result => Path.GetFullPath(result.FilePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var modifiedFiles = new List<string>();
        foreach (var page in tabs.TabPages.Cast<TabPage>())
        {
            if (PathOf(page) is not { } path || !changedPaths.Contains(Path.GetFullPath(path)) || EditorOf(page) is not { } editor)
                continue;
            if (editor.Modified)
            {
                modifiedFiles.Add(path);
                continue;
            }

            try
            {
                editor.Text = fileService.ReadText(path);
                editor.ApplyLanguage(Path.GetExtension(path));
                editor.Modified = false;
                UpdateTitle(page);
            }
            catch (Exception ex)
            {
                output.AppendText($"⚠ Impossible de recharger {path} : {ex.Message}\r\n");
            }
        }

        if (modifiedFiles.Count > 0)
        {
            output.AppendText("⚠ Fichiers ouverts avec des modifications locales non rechargés :\r\n");
            foreach (var path in modifiedFiles) output.AppendText($"  {path}\r\n");
            output.AppendText("Enregistrez ou fermez ces onglets avec attention pour éviter d’écraser le remplacement.\r\n");
        }
    }

    private void AnalyzeCurrent()
    {
        var editor = Editor();
        if (editor is null) return;

        try
        {
            var diagnostics = codeAnalysisService.Analyze(editor.Text);
            editor.SetDiagnostics(diagnostics);
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
                if (settings.LearningMode)
                    output.AppendText($"Piste : {learningHintService.GetHint(diagnostic.Code)}\r\n\r\n");
                diagnosticRanges.Add((start, output.TextLength, diagnostic.Line, diagnostic.Column));
            }

            if (settings.LearningMode)
                output.AppendText("\r\nMode apprentissage : lisez le code d’erreur, localisez la ligne indiquée et essayez une correction avant de demander l’aide de l’IA.\r\n");

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

        if (previewService.CanPreview(ext))
        {
            try
            {
                var previewPath = previewService.CreatePreviewFile(file, editor.Text);
                Process.Start(new ProcessStartInfo(previewPath) { UseShellExecute = true });
                output.Text = $"Aperçu ouvert dans le navigateur : {previewPath}";
                status.Text = "Aperçu ouvert.";
            }
            catch (Exception ex)
            {
                output.Text = $"Impossible d’ouvrir l’aperçu : {ex.Message}";
                status.Text = "Échec de l’aperçu.";
            }
            return;
        }

        var command = languageRunnerService.Resolve(file);
        var (exe, args) = command is null ? ("", "") : (command.Executable, command.Arguments);
        if (exe.Length == 0)
        {
            output.Text = $"Aucun exécuteur configuré pour {ext}.\r\nExtensions prises en charge : {string.Join(", ", languageRunnerService.SupportedExtensions.OrderBy(value => value))}.";
            return;
        }
        var toolStatuses = languageRunnerService.CheckTools(command!);
        var missingTools = toolStatuses.Where(tool => !tool.IsAvailable).ToArray();
        if (missingTools.Length > 0)
        {
            output.Text = "Exécution impossible : outil(s) manquant(s)\r\n\r\n"
                + string.Join(Environment.NewLine, missingTools.Select(tool => $"• {tool.Message}"))
                + "\r\n\r\nInstallez l’outil puis redémarrez KEYCE CODE pour actualiser le PATH.";
            status.Text = "Outil manquant.";
            return;
        }
        await RunScriptAsync(new LanguageCommand(command!.Language, exe, args), file);
    }

    private async Task RunScriptAsync(LanguageCommand command, string file)
    {
        output.Clear(); output.AppendText($"> {command.Executable} {command.Arguments}\r\n\r\n"); status.Text = "Exécution en cours…";
        try
        {
            var exitCode = await scriptRunnerService.RunAsync(command, Path.GetDirectoryName(file)!);
            output.AppendText($"\r\n[Terminé avec le code {exitCode}]");
            status.Text = exitCode == 0 ? "Exécution terminée." : $"Échec (code {exitCode})";
        }
        catch (Exception ex) { output.AppendText($"Erreur : {ex.Message}"); status.Text = "Échec de l’exécution"; }
    }

    private void ScriptOutputReceived(string text, OutputKind kind)
    {
        if (IsDisposed) return;
        BeginInvoke(() => output.AppendText(text));
    }

    private void StopCurrentRun()
    {
        if (!scriptRunnerService.IsRunning) { status.Text = "Aucun fichier en cours d’exécution."; return; }
        scriptRunnerService.Stop();
        status.Text = "Arrêt du fichier demandé.";
    }

    private void SendScriptInput()
    {
        if (!scriptRunnerService.IsRunning) { status.Text = "Aucun fichier interactif en cours."; return; }
        var text = Microsoft.VisualBasic.Interaction.InputBox("Texte à envoyer au programme :", "Entrée du programme");
        if (text.Length > 0) scriptRunnerService.SendInput(text);
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
        if (settings.ExamMode && (keyData == (Keys.Control | Keys.C) || keyData == (Keys.Control | Keys.X) || keyData == (Keys.Control | Keys.V)))
        {
            status.Text = "Presse-papiers désactivé en mode examen.";
            return true;
        }
        if (keyData == (Keys.Control | Keys.Oemplus) || keyData == (Keys.Control | Keys.Add)) { AdjustEditorFontSize(1); return true; }
        if (keyData == (Keys.Control | Keys.OemMinus) || keyData == (Keys.Control | Keys.Subtract)) { AdjustEditorFontSize(-1); return true; }
        if (keyData == (Keys.Control | Keys.D0) || keyData == (Keys.Control | Keys.NumPad0))
        {
            settings.EditorFontSize = 13;
            settingsService.Save(settings);
            foreach (var surface in tabs.TabPages.Cast<TabPage>().SelectMany(page => page.Controls.OfType<ICodeEditorSurface>()))
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
