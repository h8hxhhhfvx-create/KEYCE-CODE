namespace KeyceCode.Models;


public sealed class EditorTab
{
    public required CodeFile File { get; init; }

    public bool IsActive { get; set; }

    public bool IsPreview { get; set; }
}
