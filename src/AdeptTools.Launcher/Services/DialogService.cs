namespace AdeptTools.Launcher.Services;

public interface IDialogService
{
    string? ShowSaveFileDialog(string filter, string defaultName);
    void ShowMessage(string title, string message);
}

public class DialogService : IDialogService
{
    public string? ShowSaveFileDialog(string filter, string defaultName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = filter,
            FileName = defaultName
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public void ShowMessage(string title, string message)
    {
        System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK);
    }
}
