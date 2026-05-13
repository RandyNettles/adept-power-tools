using System.Windows;
using AdeptTools.Launcher.ViewModels;

namespace AdeptTools.Launcher.Controls;

public partial class ConfirmDeleteDialog : Window
{
    public ConfirmDeleteDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ConfirmDeleteDialogViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ConfirmDeleteDialogViewModel.DialogResult) && vm.DialogResult.HasValue)
                {
                    Close();
                }
            };
        }
    }
}
