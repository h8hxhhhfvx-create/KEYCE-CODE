using System.Drawing;

namespace KeyceCode.Views;

internal sealed class CommandPaletteForm : Form
{
    private readonly TextBox search = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher une commande…", Margin = new Padding(8) };
    private readonly ListBox results = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly IReadOnlyList<(string Label, Action Action)> commands;

    public CommandPaletteForm(IEnumerable<(string Label, Action Action)> commands)
    {
        this.commands = commands.ToArray();
        Text = "Palette de commandes — KEYCE CODE";
        Width = 620; Height = 420; StartPosition = FormStartPosition.CenterParent;
        Controls.Add(results); Controls.Add(search);
        search.TextChanged += (_, _) => RefreshResults();
        search.KeyDown += Search_KeyDown;
        results.DoubleClick += (_, _) => ExecuteSelected();
        results.KeyDown += Results_KeyDown;
        RefreshResults();
        Shown += (_, _) => search.Focus();
    }

    private void RefreshResults()
    {
        var query = search.Text.Trim();
        results.Items.Clear();
        foreach (var command in commands.Where(command => query.Length == 0 || command.Label.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
            results.Items.Add(command.Label);
        if (results.Items.Count > 0) results.SelectedIndex = 0;
    }

    private void Search_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter) { ExecuteSelected(); e.Handled = true; }
        else if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        else if (e.KeyCode == Keys.Down && results.Items.Count > 0) { results.Focus(); results.SelectedIndex = Math.Min(results.SelectedIndex + 1, results.Items.Count - 1); }
    }

    private void Results_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter) ExecuteSelected();
        else if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
    }

    private void ExecuteSelected()
    {
        if (results.SelectedItem is not string label) return;
        var command = commands.FirstOrDefault(command => command.Label == label);
        if (command.Action is null) return;
        DialogResult = DialogResult.OK;
        Close();
        command.Action();
    }
}
