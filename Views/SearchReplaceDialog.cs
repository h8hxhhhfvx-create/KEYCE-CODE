using KeyceCode.Models;

namespace KeyceCode.Views;

internal sealed class SearchReplaceDialog : Form
{
    private readonly TextBox query = new() { Dock = DockStyle.Fill };
    private readonly TextBox replacement = new() { Dock = DockStyle.Fill };
    private readonly CheckBox matchCase = new() { Text = "Respecter la casse", AutoSize = true };
    private readonly CheckBox regularExpression = new() { Text = "Expression régulière", AutoSize = true };
    private readonly CheckBox wholeWord = new() { Text = "Mot entier", AutoSize = true };

    public SearchReplaceDialog(bool replaceMode)
    {
        Text = replaceMode ? "Remplacer" : "Rechercher";
        Width = 520;
        Height = replaceMode ? 230 : 190;
        MinimumSize = new Size(440, Height);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = replaceMode ? 5 : 4 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.Controls.Add(new Label { Text = "Rechercher :", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        table.Controls.Add(query, 1, 0);
        if (replaceMode)
        {
            table.Controls.Add(new Label { Text = "Remplacer par :", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            table.Controls.Add(replacement, 1, 1);
        }

        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        options.Controls.Add(matchCase);
        options.Controls.Add(regularExpression);
        options.Controls.Add(wholeWord);
        table.Controls.Add(options, 0, replaceMode ? 2 : 1);
        table.SetColumnSpan(options, 2);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true };
        var cancel = new Button { Text = "Annuler", AutoSize = true, DialogResult = DialogResult.Cancel };
        var accept = new Button { Text = replaceMode ? "Remplacer tout" : "Rechercher", AutoSize = true, DialogResult = DialogResult.OK };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(accept);
        table.Controls.Add(buttons, 0, replaceMode ? 4 : 3);
        table.SetColumnSpan(buttons, 2);
        Controls.Add(table);
        AcceptButton = accept;
        CancelButton = cancel;
    }

    public string Query => query.Text;
    public string Replacement => replacement.Text;
    public TextSearchOptions Options => new(matchCase.Checked, regularExpression.Checked, wholeWord.Checked);
}
