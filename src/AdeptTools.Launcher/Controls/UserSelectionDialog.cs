using System.Windows;
using AdeptTools.Launcher.ViewModels;

namespace AdeptTools.Launcher.Controls;

public partial class UserSelectionDialog : Window
{
    public UserSelectionDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is UserSelectionDialogViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(UserSelectionDialogViewModel.CloseRequested) && vm.CloseRequested)
                    Close();
            };
        }
    }
}
