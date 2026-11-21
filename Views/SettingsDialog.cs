using KeyceCode.Models;

namespace KeyceCode.Views;

internal sealed class SettingsDialog : Form
{
    private readonly ComboBox theme = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown fontSize = new() { Minimum = 8, Maximum = 32, DecimalPlaces = 0 };
    private readonly ComboBox tabSize = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox wordWrap = new() { Text = "Retour automatique à la ligne" };
    private readonly CheckBox learningMode = new() { Text = "Mode apprentissage" };
    private readonly CheckBox examMode = new() { Text = "Mode examen" };
    private readonly TextBox endpoint = new();
    private readonly TextBox model = new();

    public SettingsDialog(AppSettings current)
    {
        Text = "Paramètres — KEYCE CODE";
        Width = 540; Height = 430; MinimumSize = new Size(500, 380);
        StartPosition = FormStartPosition.CenterParent;
        theme.Items.AddRange(["Dark", "Light"]); theme.SelectedItem = current.Theme;
        fontSize.Value = (decimal)Math.Clamp(current.EditorFontSize, 8, 32);
        tabSize.Items.AddRange([2, 4, 8]); tabSize.SelectedItem = current.TabSize is 2 or 4 or 8 ? current.TabSize : 4;
        wordWrap.Checked = current.WordWrap; learningMode.Checked = current.LearningMode; examMode.Checked = current.ExamMode;
        endpoint.Text = current.OllamaEndpoint; model.Text = current.OllamaModel;

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 2, RowCount = 9, AutoSize = true };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, 0, "Thème", theme); AddRow(table, 1, "Taille du texte", fontSize); AddRow(table, 2, "Tabulations", tabSize);
        table.Controls.Add(wordWrap, 1, 3); table.Controls.Add(learningMode, 1, 4); table.Controls.Add(examMode, 1, 5);
        AddRow(table, 6, "Adresse Ollama", endpoint); AddRow(table, 7, "Modèle Ollama", model);
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true };
        var ok = new Button { Text = "Enregistrer", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(ok); buttons.Controls.Add(cancel); table.Controls.Add(buttons, 1, 8);
        Controls.Add(table); AcceptButton = ok; CancelButton = cancel;
        ok.Click += (_, _) => { if (examMode.Checked) learningMode.Checked = false; else if (learningMode.Checked) examMode.Checked = false; };
    }

    public AppSettings GetSettings() => new()
    {
        Theme = theme.SelectedItem?.ToString() ?? "Dark",
        EditorFontSize = (double)fontSize.Value,
        TabSize = Convert.ToInt32(tabSize.SelectedItem ?? 4),
        WordWrap = wordWrap.Checked,
        LearningMode = learningMode.Checked,
        ExamMode = examMode.Checked,
        OllamaEndpoint = endpoint.Text.Trim().TrimEnd('/'),
        OllamaModel = model.Text.Trim()
    };

    private static void AddRow(TableLayoutPanel table, int row, string label, Control control)
    {
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 5, 0, 0) }, 0, row);
        control.Dock = DockStyle.Fill; table.Controls.Add(control, 1, row);
    }
}
