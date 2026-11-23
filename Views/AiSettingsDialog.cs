using KeyceCode.Models;
using KeyceCode.Services;
using System.Net.Http;

namespace KeyceCode.Views;

internal sealed class AiSettingsDialog : Form
{
    private readonly ISecretStore secrets;
    private readonly TextBox geminiModel = new();
    private readonly TextBox groqModel = new();
    private readonly TextBox gemini1 = SecretBox();
    private readonly TextBox gemini2 = SecretBox();
    private readonly TextBox groq1 = SecretBox();
    private readonly TextBox groq2 = SecretBox();
    private readonly CheckBox clearKeys = new() { Text = "Effacer les clés enregistrées", AutoSize = true };

    public AiSettingsDialog(AppSettings settings, ISecretStore secrets)
    {
        this.secrets = secrets;
        Text = "Configuration IA — KEYCE CODE";
        Width = 620; Height = 430; MinimumSize = new Size(580, 390);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent;
        geminiModel.Text = settings.GeminiModel; groqModel.Text = settings.GroqModel;
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 2, RowCount = 9, AutoSize = true };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, 0, "Modèle Gemini", geminiModel); AddRow(table, 1, "Clé Gemini 1", gemini1); AddRow(table, 2, "Clé Gemini 2", gemini2);
        AddRow(table, 3, "Modèle Groq", groqModel); AddRow(table, 4, "Clé Groq 1", groq1); AddRow(table, 5, "Clé Groq 2", groq2);
        table.Controls.Add(new Label { Text = "Laisser une clé vide pour la conserver.", AutoSize = true }, 1, 6);
        table.Controls.Add(clearKeys, 1, 7);
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true };
        var ok = new Button { Text = "Enregistrer", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, AutoSize = true };
        var test = new Button { Text = "Tester les connexions", AutoSize = true };
        test.Click += async (_, _) => await TestConnectionsAsync();
        buttons.Controls.Add(ok); buttons.Controls.Add(cancel); buttons.Controls.Add(test); table.Controls.Add(buttons, 1, 8);
        Controls.Add(table); AcceptButton = ok; CancelButton = cancel;
    }

    public (string GeminiModel, string GroqModel) GetModels() => (
        string.IsNullOrWhiteSpace(geminiModel.Text) ? "gemini-2.5-flash" : geminiModel.Text.Trim(),
        string.IsNullOrWhiteSpace(groqModel.Text) ? "llama-3.1-8b-instant" : groqModel.Text.Trim());

    public void SaveSecrets()
    {
        if (clearKeys.Checked)
        {
            secrets.Delete(AiProviderFactory.GeminiKey1); secrets.Delete(AiProviderFactory.GeminiKey2);
            secrets.Delete(AiProviderFactory.GroqKey1); secrets.Delete(AiProviderFactory.GroqKey2);
            return;
        }
        SaveIfEntered(AiProviderFactory.GeminiKey1, gemini1); SaveIfEntered(AiProviderFactory.GeminiKey2, gemini2);
        SaveIfEntered(AiProviderFactory.GroqKey1, groq1); SaveIfEntered(AiProviderFactory.GroqKey2, groq2);
    }

    private async Task TestConnectionsAsync()
    {
        var models = GetModels();
        var candidates = new[]
        {
            ("Gemini 1", "https://generativelanguage.googleapis.com/v1beta/openai", models.GeminiModel, Value(gemini1, AiProviderFactory.GeminiKey1)),
            ("Gemini 2", "https://generativelanguage.googleapis.com/v1beta/openai", models.GeminiModel, Value(gemini2, AiProviderFactory.GeminiKey2)),
            ("Groq 1", "https://api.groq.com/openai/v1", models.GroqModel, Value(groq1, AiProviderFactory.GroqKey1)),
            ("Groq 2", "https://api.groq.com/openai/v1", models.GroqModel, Value(groq2, AiProviderFactory.GroqKey2))
        };
        var results = new List<string>();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        foreach (var (name, endpoint, model, key) in candidates)
        {
            if (string.IsNullOrWhiteSpace(key)) { results.Add($"{name} : non configuré"); continue; }
            try
            {
                var provider = new OpenAiCompatibleChatProvider(name, endpoint, key, client);
                await provider.ChatAsync(model, [("user", "Réponds uniquement OK.")]);
                results.Add($"{name} : connexion réussie");
            }
            catch (Exception ex) { results.Add($"{name} : échec ({ex.Message})"); }
        }
        MessageBox.Show(this, string.Join(Environment.NewLine, results), "Test des connexions IA", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private string? Value(TextBox field, string name) => string.IsNullOrWhiteSpace(field.Text) ? secrets.Get(name) : field.Text.Trim();

    public void ApplyTheme(bool dark)
    {
        var surface = dark ? Color.FromArgb(37, 40, 48) : Color.FromArgb(248, 249, 251);
        var field = dark ? Color.FromArgb(50, 54, 64) : Color.White;
        var text = dark ? Color.Gainsboro : Color.FromArgb(32, 35, 40);
        BackColor = surface; ForeColor = text; ApplyTheme(this, surface, field, text);
    }

    private void SaveIfEntered(string name, TextBox field) { if (!string.IsNullOrWhiteSpace(field.Text)) secrets.Set(name, field.Text.Trim()); }
    private static TextBox SecretBox() => new() { UseSystemPasswordChar = true };
    private static void AddRow(TableLayoutPanel table, int row, string label, Control control)
    { table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 5, 0, 0) }, 0, row); control.Dock = DockStyle.Fill; table.Controls.Add(control, 1, row); }
    private static void ApplyTheme(Control parent, Color surface, Color field, Color text)
    { foreach (Control child in parent.Controls) { child.ForeColor = text; child.BackColor = child is TextBox ? field : surface; if (child.HasChildren) ApplyTheme(child, surface, field, text); } }
}
