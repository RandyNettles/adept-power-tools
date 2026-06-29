using System.Windows;
using AdeptTools.Launcher.ViewModels;

namespace AdeptTools.Launcher.Controls;

public partial class ComProfileDialog : Window
{
    public ComProfileDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ComProfileDialogViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ComProfileDialogViewModel.DialogResult) && vm.DialogResult.HasValue)
                    Close();
            };
        }
    }
}
