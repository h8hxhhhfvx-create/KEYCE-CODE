using KeyceCode.Models;
using KeyceCode.ViewModels;

namespace KeyceCode.Views;

public sealed class ExplorerView : UserControl
{
    private readonly ProjectViewModel viewModel;
    private readonly TreeView tree = new()
    {
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.None,
        BackColor = Color.FromArgb(30, 33, 40),
        ForeColor = Color.Gainsboro,
        ShowRootLines = false,
        HideSelection = false
    };

    public event Action<string>? FileOpenRequested;
    public event Action<string, string>? ItemRenamed;
    public event Func<string, bool>? BeforeDelete;
    public event Action<Project>? ProjectOpened;

    public ExplorerView(ProjectViewModel viewModel)
    {
        this.viewModel = viewModel;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(30, 33, 40);

        var openButton = new Button
        {
            Text = "Ouvrir un projet C#…", Dock = DockStyle.Top, Height = 36,
            FlatStyle = FlatStyle.Flat, ForeColor = Color.Gainsboro,
            BackColor = Color.FromArgb(45, 49, 58), TextAlign = ContentAlignment.MiddleLeft
        };
        openButton.FlatAppearance.BorderSize = 0;
        openButton.Click += (_, _) => OpenProjectDialog((IWin32Window?)FindForm() ?? this);

        var title = new Label
        {
            Text = "  EXPLORATEUR", Dock = DockStyle.Top, Height = 32,
            TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Silver,
            BackColor = Color.FromArgb(30, 33, 40), Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        Controls.Add(tree);
        Controls.Add(openButton);
        Controls.Add(title);

        tree.NodeMouseDoubleClick += (_, e) =>
        {
            if (e.Node?.Tag is CodeFile { IsDirectory: false } file && File.Exists(file.FullPath))
                FileOpenRequested?.Invoke(file.FullPath);
        };
        tree.NodeMouseClick += (_, e) => { if (e.Button == MouseButtons.Right) tree.SelectedNode = e.Node; };
        tree.ContextMenuStrip = BuildContextMenu();
    }

    public void OpenProjectDialog(IWin32Window owner)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Ouvrir une solution ou un projet C#",
            Filter = "Projets C# (*.sln;*.slnx;*.csproj)|*.sln;*.slnx;*.csproj|Tous les fichiers (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(owner) == DialogResult.OK) OpenProject(dialog.FileName);
    }

    public void OpenProject(string path)
    {
        try
        {
            var project = viewModel.OpenProject(path);
            RenderProject();
            ProjectOpened?.Invoke(project);
        }
        catch (Exception ex) { ShowError("Impossible d’ouvrir le projet", ex); }
    }

    public void RefreshProject()
    {
        try { viewModel.Refresh(); RenderProject(); }
        catch (Exception ex) { ShowError("Impossible d’actualiser l’explorateur", ex); }
    }

    private void RenderProject()
    {
        tree.BeginUpdate();
        tree.Nodes.Clear();
        if (viewModel.CurrentProject is { } project)
        {
            var rootFile = new CodeFile { Name = project.Name, FullPath = project.RootPath, IsDirectory = true };
            rootFile.Children.AddRange(project.Files);
            var root = AddNode(rootFile);
            tree.Nodes.Add(root);
            root.Expand();
        }
        tree.EndUpdate();
    }

    private static TreeNode AddNode(CodeFile file)
    {
        var node = new TreeNode(file.Name) { Tag = file };
        foreach (var child in file.Children) node.Nodes.Add(AddNode(child));
        return node;
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Nouveau fichier…", null, (_, _) => CreateFile());
        menu.Items.Add("Nouveau dossier…", null, (_, _) => CreateDirectory());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Renommer…", null, (_, _) => RenameSelected());
        menu.Items.Add("Supprimer", null, (_, _) => DeleteSelected());
        menu.Opening += (_, _) =>
        {
            var selected = SelectedFile();
            var isRoot = selected is not null && viewModel.CurrentProject is { } project &&
                         string.Equals(selected.FullPath, project.RootPath, StringComparison.OrdinalIgnoreCase);
            menu.Items[0].Enabled = selected?.IsDirectory == true;
            menu.Items[1].Enabled = selected?.IsDirectory == true;
            menu.Items[3].Enabled = selected is not null && !isRoot;
            menu.Items[4].Enabled = selected is not null && !isRoot;
        };
        return menu;
    }

    private CodeFile? SelectedFile() => tree.SelectedNode?.Tag as CodeFile;

    private void CreateFile()
    {
        if (SelectedFile() is not { IsDirectory: true } folder) return;
        var name = Microsoft.VisualBasic.Interaction.InputBox("Nom du fichier (ex. Program.cs) :", "Nouveau fichier");
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            var file = viewModel.CreateFile(folder.FullPath, name.Trim());
            RenderProject(); FileOpenRequested?.Invoke(file.FullPath);
        }
        catch (Exception ex) { ShowError("Impossible de créer le fichier", ex); }
    }

    private void CreateDirectory()
    {
        if (SelectedFile() is not { IsDirectory: true } folder) return;
        var name = Microsoft.VisualBasic.Interaction.InputBox("Nom du nouveau dossier :", "Nouveau dossier");
        if (string.IsNullOrWhiteSpace(name)) return;
        try { viewModel.CreateDirectory(folder.FullPath, name.Trim()); RenderProject(); }
        catch (Exception ex) { ShowError("Impossible de créer le dossier", ex); }
    }

    private void RenameSelected()
    {
        if (SelectedFile() is not { } file) return;
        var name = Microsoft.VisualBasic.Interaction.InputBox("Nouveau nom :", "Renommer", file.Name);
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name.Trim(), file.Name, StringComparison.Ordinal)) return;
        try
        {
            var newPath = viewModel.Rename(file.FullPath, name.Trim());
            ItemRenamed?.Invoke(file.FullPath, newPath);
            RenderProject();
        }
        catch (Exception ex) { ShowError("Impossible de renommer l’élément", ex); }
    }

    private void DeleteSelected()
    {
        if (SelectedFile() is not { } file) return;
        var isDirectory = file.IsDirectory;
        var answer = MessageBox.Show(this,
            $"Supprimer « {file.Name} »{(isDirectory ? " et tout son contenu" : "")} ?",
            "Confirmer la suppression", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes || BeforeDelete?.Invoke(file.FullPath) == false) return;
        try { viewModel.Delete(file.FullPath, recursive: isDirectory); RenderProject(); }
        catch (Exception ex) { ShowError("Impossible de supprimer l’élément", ex); }
    }

    private void ShowError(string title, Exception exception) =>
        MessageBox.Show(this, $"{exception.Message}", title, MessageBoxButtons.OK, MessageBoxIcon.Error);
}
