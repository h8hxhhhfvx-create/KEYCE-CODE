namespace KeyceCode.Models;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public double EditorFontSize { get; set; } = 13;
    public bool WordWrap { get; set; }
    public int TabSize { get; set; } = 4;
    public bool AutoSave { get; set; }
    public int AutoSaveDelaySeconds { get; set; } = 60;
    public bool FormatOnSave { get; set; }
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "qwen2.5:7b";
    public string GeminiModel { get; set; } = "gemini-2.5-flash";
    public string GroqModel { get; set; } = "llama-3.1-8b-instant";
    public string? DebuggerPath { get; set; }
    public bool LearningMode { get; set; }
    public bool ExamMode { get; set; }
    public List<string> RecentProjects { get; set; } = [];
    public string? LastWorkspace { get; set; }
    public List<string> OpenFiles { get; set; } = [];
}
