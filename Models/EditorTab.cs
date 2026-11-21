namespace KeyceCode.Models;

/// <summary>
/// Represents one document displayed in the editor tab strip.
/// </summary>
public sealed class EditorTab
{
    public required CodeFile File { get; init; }

    public bool IsActive { get; set; }

    public bool IsPreview { get; set; }
}
