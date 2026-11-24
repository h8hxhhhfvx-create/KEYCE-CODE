using KeyceCode.ViewModels;

namespace KeyceCode.Views;
 public 

public partial class OutputView
{
   
    }

    private void ScrollToEnd()
    {
        if (LinesList.Items.Count > 0)
            LinesList.ScrollIntoView(LinesList.Items[LinesList.Items.Count - 1]);
    }
}
