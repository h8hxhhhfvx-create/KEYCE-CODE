namespace KeyceCode.Models;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public double EditorFontSize { get; set; } = 13;
    public bool WordWrap { get; set; }
    public int TabSize { get; set; } = 4;
}
