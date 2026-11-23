using KeyceCode.Models;

namespace KeyceCode.Views;

/// <summary>
/// Vue latérale dédiée au contrôle de code source, inspirée du panneau Source Control
/// des éditeurs modernes. Elle ne connaît pas Git directement : les actions sont
/// remontées à Form1, qui les exécute via GitService.
/// </summary>
public sealed class SourceControlView : UserControl
{
    private readonly Label repositoryLabel = new() { Dock = DockStyle.Top, Height = 26, Padding = new Padding(8, 5, 4, 0), ForeColor = Color.Silver, Text = "Aucun dépôt ouvert" };
    private readonly Label branchLabel = new() { Dock = DockStyle.Top, Height = 26, Padding = new Padding(8, 3, 4, 0), ForeColor = Color.LightSkyBlue };
    private readonly ListView files = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(30, 33, 40), ForeColor = Color.Gainsboro, HeaderStyle = ColumnHeaderStyle.Nonclickable };
    private readonly ToolStripStatusLabel countLabel = new("0 changement") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripButton stageButton = new("Indexer") { Enabled = false };
    private readonly ToolStripButton unstageButton = new("Désindexer") { Enabled = false };
    private readonly ToolStripButton diffButton = new("Diff") { Enabled = false };

    public event Action? RefreshRequested;
    public event Action? CommitRequested;
    public event Action<string>? StageRequested;
    public event Action<string>? UnstageRequested;
    public event Action<string, bool>? DiffRequested;

    public SourceControlView()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(30, 33, 40);

        files.Columns.Add("État", 58);
        files.Columns.Add("Fichier", 250);
        files.SelectedIndexChanged += (_, _) => UpdateButtons();
        files.DoubleClick += (_, _) =>
        {
            if (SelectedFile is { } selected) DiffRequested?.Invoke(selected.Path, selected.IndexStatus != " ");
        };

        var actions = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, BackColor = Color.FromArgb(30, 33, 40), ForeColor = Color.Gainsboro, Renderer = new ToolStripProfessionalRenderer(new DarkColors()) };
        var refresh = new ToolStripButton("Actualiser") { ToolTipText = "Actualiser l’état Git" };
        refresh.Click += (_, _) => RefreshRequested?.Invoke();
        stageButton.Click += (_, _) => { if (SelectedFile is { } file) StageRequested?.Invoke(file.Path); };
        unstageButton.Click += (_, _) => { if (SelectedFile is { } file) UnstageRequested?.Invoke(file.Path); };
        diffButton.Click += (_, _) => { if (SelectedFile is { } file) DiffRequested?.Invoke(file.Path, file.IndexStatus != " "); };
        var commit = new ToolStripButton("Commit") { ToolTipText = "Créer un commit avec les changements indexés" };
        commit.Click += (_, _) => CommitRequested?.Invoke();
        actions.Items.AddRange([refresh, new ToolStripSeparator(), stageButton, unstageButton, diffButton, new ToolStripSeparator(), commit]);

        var status = new StatusStrip { Dock = DockStyle.Bottom, BackColor = Color.FromArgb(30, 33, 40), ForeColor = Color.Silver };
        status.Items.Add(countLabel);

        Controls.Add(files);
        Controls.Add(status);
        Controls.Add(branchLabel);
        Controls.Add(repositoryLabel);
        Controls.Add(actions);
    }

    public GitFileStatus? SelectedFile => files.SelectedItems.Count == 0 ? null : files.SelectedItems[0].Tag as GitFileStatus;

    public void ClearRepository()
    {
        repositoryLabel.Text = "Aucun dépôt ouvert";
        branchLabel.Text = string.Empty;
        files.Items.Clear();
        countLabel.Text = "0 changement";
        UpdateButtons();
    }

    public void ApplySnapshot(GitStatusSnapshot snapshot)
    {
        repositoryLabel.Text = "CONTRÔLE DE CODE SOURCE";
        branchLabel.Text = $"Branche : {snapshot.Branch}";
        files.BeginUpdate();
        try
        {
            files.Items.Clear();
            var staged = files.Groups.Add("staged", "Changements indexés");
            var working = files.Groups.Add("working", "Changements");
            foreach (var file in snapshot.Files.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
            {
                bool isStaged = file.IndexStatus != " " && file.IndexStatus != "?";
                var item = new ListViewItem(DisplayStatus(file)) { Tag = file, ForeColor = isStaged ? Color.LightGreen : Color.Khaki, Group = isStaged ? staged : working };
                item.SubItems.Add(file.Path);
                files.Items.Add(item);
            }
            countLabel.Text = snapshot.Files.Count == 1 ? "1 changement" : $"{snapshot.Files.Count} changements";
        }
        finally { files.EndUpdate(); }
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var selected = SelectedFile;
        stageButton.Enabled = selected is not null && (selected.WorkTreeStatus != " " || selected.IndexStatus == "?");
        unstageButton.Enabled = selected is not null && selected.IndexStatus != " " && selected.IndexStatus != "?";
        diffButton.Enabled = selected is not null;
    }

    private static string DisplayStatus(GitFileStatus file) => file.IndexStatus == "?" ? "?" : $"{file.IndexStatus}{file.WorkTreeStatus}";

    private sealed class DarkColors : ProfessionalColorTable
    {
        public override Color MenuBorder => Color.FromArgb(70, 74, 84);
        public override Color MenuItemSelected => Color.FromArgb(55, 61, 72);
        public override Color ToolStripDropDownBackground => Color.FromArgb(30, 33, 40);
        public override Color ImageMarginGradientBegin => Color.FromArgb(30, 33, 40);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(30, 33, 40);
        public override Color ImageMarginGradientEnd => Color.FromArgb(30, 33, 40);
    }
}
