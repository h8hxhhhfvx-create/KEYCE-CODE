using System.ComponentModel;
using System.Runtime.CompilerServices;
using KeyceCode.Models;

namespace KeyceCode.ViewModels;

public sealed class EditorViewModel : INotifyPropertyChanged
{
    private EditorTab? activeTab;

    public BindingList<EditorTab> Tabs { get; } = [];

    public EditorTab? ActiveTab
    {
        get => activeTab;
        set
        {
            if (ReferenceEquals(activeTab, value)) return;
            if (activeTab is not null) activeTab.IsActive = false;
            activeTab = value;
            if (activeTab is not null) activeTab.IsActive = true;
            OnPropertyChanged();
        }
    }

    public void AddTab(EditorTab tab)
    {
        Tabs.Add(tab);
        ActiveTab = tab;
    }

    public void RemoveTab(EditorTab tab)
    {
        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        if (ReferenceEquals(ActiveTab, tab))
            ActiveTab = Tabs.Count == 0 ? null : Tabs[Math.Min(index, Tabs.Count - 1)];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
