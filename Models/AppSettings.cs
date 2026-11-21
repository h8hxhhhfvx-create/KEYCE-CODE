namespace KeyceCode.Models;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public double EditorFontSize { get; set; } = 13;
    public bool WordWrap { get; set; }
    public int TabSize { get; set; } = 4;
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "qwen2.5:7b";
    public bool LearningMode { get; set; }
    public bool ExamMode { get; set; }
    public List<string> RecentProjects { get; set; } = [];
}
