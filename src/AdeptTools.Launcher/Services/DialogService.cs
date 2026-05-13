namespace AdeptTools.Launcher.Services;

public interface IDialogService
{
    string? ShowSaveFileDialog(string filter, string defaultName);
    string? ShowOpenFileDialog(string filter);
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

    public string? ShowOpenFileDialog(string filter)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = filter
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public void ShowMessage(string title, string message)
    {
        System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK);
    }
}
