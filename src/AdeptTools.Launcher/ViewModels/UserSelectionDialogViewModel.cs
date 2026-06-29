using AdeptTools.Core.Auth;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AdeptTools.Launcher.ViewModels;

public partial class UserSelectionDialogViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SelectCommand))]
    private UserChoice? _selectedUser;

    [ObservableProperty]
    private bool _closeRequested;

    public IReadOnlyList<UserChoice> Users { get; }

    public bool Confirmed { get; private set; }

    public UserSelectionDialogViewModel(IReadOnlyList<UserChoice> users)
    {
        Users = users;
        if (users.Count > 0)
            _selectedUser = users[0];
    }

    [RelayCommand(CanExecute = nameof(CanSelect))]
    private void Select()
    {
        Confirmed = true;
        CloseRequested = true;
    }

    private bool CanSelect() => SelectedUser is not null;

    [RelayCommand]
    private void Cancel()
    {
        Confirmed = false;
        CloseRequested = true;
    }
}
