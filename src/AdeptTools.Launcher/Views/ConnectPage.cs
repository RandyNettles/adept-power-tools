using System.Windows.Controls;
using AdeptTools.Launcher.ViewModels;

namespace AdeptTools.Launcher.Views;

public partial class ConnectPage : UserControl
{
    public ConnectPage()
    {
        InitializeComponent();
    }

    private void PasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ConnectViewModel vm)
            vm.SetPassword(((PasswordBox)sender).Password);
    }
}
