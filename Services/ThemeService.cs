using System.Drawing;
using System.Windows.Forms.Integration;
using KeyceCode.Views;

namespace KeyceCode.Services;

public enum AppTheme { Dark, Light }

public sealed class ThemeService
{
    public AppTheme Current { get; private set; } = AppTheme.Dark;
    public Color Background => Current == AppTheme.Dark ? Color.FromArgb(37, 40, 48) : Color.FromArgb(245, 247, 250);
    public Color Surface => Current == AppTheme.Dark ? Color.FromArgb(30, 33, 40) : Color.FromArgb(255, 255, 255);
    public Color Panel => Current == AppTheme.Dark ? Color.FromArgb(20, 22, 27) : Color.FromArgb(238, 241, 245);
    public Color Foreground => Current == AppTheme.Dark ? Color.Gainsboro : Color.FromArgb(35, 40, 48);
    public Color Accent => Current == AppTheme.Dark ? Color.FromArgb(0, 104, 156) : Color.FromArgb(0, 116, 180);

    public void SetTheme(AppTheme theme) => Current = theme;

    public void Apply(Control root)
    {
        root.BackColor = Background; root.ForeColor = Foreground;
        foreach (Control control in root.Controls) ApplyControl(control);
    }

    private void ApplyControl(Control control)
    {
        switch (control)
        {
            case StatusStrip:
                control.BackColor = Accent; control.ForeColor = Color.White; break;
            case MenuStrip or ToolStrip:
                control.BackColor = Surface; control.ForeColor = Foreground; break;
            case TreeView:
            case TabControl:
                control.BackColor = Surface; control.ForeColor = Foreground; break;
            case RichTextBox:
                control.BackColor = Panel; control.ForeColor = Foreground; break;
            case Label:
                control.BackColor = Surface; control.ForeColor = Foreground; break;
            case Button:
                control.BackColor = Surface; control.ForeColor = Foreground; break;
            case System.Windows.Forms.Panel or SplitContainer or TabPage:
                control.BackColor = Background; control.ForeColor = Foreground; break;
            default:
                control.BackColor = Background; control.ForeColor = Foreground; break;
        }
        if (control is CodeEditorSurface editor) editor.SetTheme(Current == AppTheme.Dark);
        if (control is ElementHost host && host.Child is System.Windows.FrameworkElement wpfRoot)
            ApplyWpfTheme(wpfRoot);
        foreach (Control child in control.Controls) ApplyControl(child);
    }

    private void ApplyWpfTheme(System.Windows.DependencyObject root)
    {
        var background = ToWpfColor(Surface);
        var foreground = ToWpfColor(Foreground);
        switch (root)
        {
            case System.Windows.Controls.Control wpfControl:
                wpfControl.SetCurrentValue(System.Windows.Controls.Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush(background));
                wpfControl.SetCurrentValue(System.Windows.Controls.Control.ForegroundProperty, new System.Windows.Media.SolidColorBrush(foreground));
                break;
            case System.Windows.Controls.Panel panel:
                panel.SetCurrentValue(System.Windows.Controls.Panel.BackgroundProperty, new System.Windows.Media.SolidColorBrush(background));
                break;
            case System.Windows.Controls.Border border:
                border.SetCurrentValue(System.Windows.Controls.Border.BackgroundProperty, new System.Windows.Media.SolidColorBrush(background));
                break;
            case System.Windows.Controls.TextBlock textBlock:
                textBlock.SetCurrentValue(System.Windows.Controls.TextBlock.ForegroundProperty, new System.Windows.Media.SolidColorBrush(foreground));
                break;
        }

        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            ApplyWpfTheme(System.Windows.Media.VisualTreeHelper.GetChild(root, i));
    }

    private static System.Windows.Media.Color ToWpfColor(Color color) => System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
}
