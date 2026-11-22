using KeyceCode.Services;

namespace KeyceCode.Views;

/// <summary>Fenêtre de préférences visuelles de KeyceCode.</summary>
public sealed class SettingsView : Form
{
    private readonly ComboBox themeSelector = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 220
    };

    public AppTheme SelectedTheme => themeSelector.SelectedIndex == 1 ? AppTheme.Light : AppTheme.Dark;

    public SettingsView(AppTheme currentTheme)
    {
        Text = "Paramètres";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(360, 160);

        var title = new Label
        {
            Text = "Apparence",
            AutoSize = true,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Location = new Point(20, 20)
        };
        var themeLabel = new Label { Text = "Thème de l’application", AutoSize = true, Location = new Point(20, 64) };
        themeSelector.Items.AddRange(["Sombre", "Clair"]);
        themeSelector.SelectedIndex = currentTheme == AppTheme.Dark ? 0 : 1;
        themeSelector.Location = new Point(150, 59);

        var apply = new Button { Text = "Appliquer", DialogResult = DialogResult.OK, Location = new Point(168, 112), Width = 82 };
        var cancel = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, Location = new Point(258, 112), Width = 82 };
        Controls.AddRange([title, themeLabel, themeSelector, apply, cancel]);
        AcceptButton = apply;
        CancelButton = cancel;
    }
}
