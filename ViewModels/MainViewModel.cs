using KeyceCode.Services;

namespace KeyceCode.ViewModels;

public sealed class MainViewModel
{
    public ThemeService ThemeService { get; } = new();
    public OutputViewModel Output { get; } = new();
    public TerminalViewModel Terminal { get; } = new();
    public bool ExplorerVisible { get; set; } = true;
    public bool OutputVisible { get; set; } = true;
}
