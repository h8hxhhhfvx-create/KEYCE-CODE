namespace KeyceCode.Views;

public partial class AiChatView
{
    public AiChatView() => InitializeComponent();

    public void SetExamMode(bool enabled)
    {
        InputBox.IsReadOnly = enabled;
        InputBox.Text = enabled ? string.Empty : InputBox.Text;
        SendButton.IsEnabled = !enabled;
    }

    public void ApplyTheme(bool dark)
    {
        var background = dark ? "#1E2128" : "#FAFAFA";
        var foreground = dark ? "#DCDCDC" : "#202020";
        var inputBackground = dark ? "#30343D" : "#FFFFFF";
        var border = dark ? "#566070" : "#B8C0CC";
        Root.Background = Brush(background);
        MessagesList.Background = Brush(background);
        MessagesList.Foreground = Brush(foreground);
        InputBox.Background = Brush(inputBackground);
        InputBox.Foreground = Brush(foreground);
        InputBox.BorderBrush = Brush(border);
    }

    private static System.Windows.Media.SolidColorBrush Brush(string value) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value));
}
