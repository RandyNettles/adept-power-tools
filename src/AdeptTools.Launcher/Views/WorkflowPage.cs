using System.Windows.Controls;
using AdeptTools.Launcher.ViewModels;

namespace AdeptTools.Launcher.Views;

public partial class WorkflowPage : UserControl
{
    public WorkflowPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is WorkflowViewModel vm)
                vm.OnNavigatedTo();
        };
    }
}
