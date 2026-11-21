using KeyceCode.ViewModels;

namespace KeyceCode.Views;

public partial class TerminalView
{
    public TerminalView()
    {
        InitializeComponent();

        // Défilement automatique vers la dernière ligne.
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is TerminalViewModel vm)
                vm.Log.Lines.CollectionChanged += (_, _) => ScrollToEnd();
        };
    }

    private void ScrollToEnd()
    {
        if (LinesList.Items.Count > 0)
            LinesList.ScrollIntoView(LinesList.Items[LinesList.Items.Count - 1]);
    }
}
