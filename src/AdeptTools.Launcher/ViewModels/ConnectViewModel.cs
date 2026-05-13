using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AdeptTools.Core.Auth;
using AdeptTools.Core.Models;
using AdeptTools.Launcher.Converters;
using AdeptTools.Launcher.Services;

namespace AdeptTools.Launcher.ViewModels;

public partial class ConnectViewModel : ObservableObject
{
    private readonly Func<IAdeptAuthService> _authServiceFactory;
    private readonly MockModeState _mockModeState;
    private string _password = string.Empty;

    public void SetPassword(string password) => _password = password;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
    private string _serverUrl = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
    private string _userName = string.Empty;

    [ObservableProperty]
    private BackendType _selectedBackend = BackendType.Http;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHttpFields))]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
    private bool _isMockMode;

    [ObservableProperty]
    private ConnectionStatus _status = ConnectionStatus.Disconnected;

    public event EventHandler<ConnectionStatus>? StatusChanged;

    [ObservableProperty]
    private string _statusText = "Not connected";

    [ObservableProperty]
    private string? _displayName;

    [ObservableProperty]
    private string? _serverVersion;

    [ObservableProperty]
    private string? _errorMessage;

    public bool ShowHttpFields => SelectedBackend == BackendType.Http && !IsMockMode;

    public ConnectViewModel(Func<IAdeptAuthService> authServiceFactory, MockModeState mockModeState)
    {
        _authServiceFactory = authServiceFactory;
        _mockModeState = mockModeState;
        _isMockMode = _mockModeState.IsMock;

        _mockModeState.Changed += (_, isMock) =>
        {
            IsMockMode = isMock;
        };
    }

    partial void OnSelectedBackendChanged(BackendType value)
    {
        OnPropertyChanged(nameof(ShowHttpFields));
        TestConnectionCommand.NotifyCanExecuteChanged();
        ResetConnectionStatus();
    }

    partial void OnIsMockModeChanged(bool value)
    {
        _mockModeState.IsMock = value;
        ResetConnectionStatus();
    }

    private bool CanTestConnection()
    {
        if (IsMockMode) return true;
        if (SelectedBackend == BackendType.Com) return true;
        return !string.IsNullOrWhiteSpace(ServerUrl) && !string.IsNullOrWhiteSpace(UserName);
    }

    [RelayCommand(CanExecute = nameof(CanTestConnection))]
    private async Task TestConnectionAsync()
    {
        Status = ConnectionStatus.Connecting;
        StatusText = "Connecting...";
        ErrorMessage = null;
        DisplayName = null;
        ServerVersion = null;

        try
        {
            var authService = _authServiceFactory();
            var url = IsMockMode ? "mock://localhost" : ServerUrl;
            var user = IsMockMode ? "mock-user" : UserName;

            var result = await authService.LoginAsync(url, user, _password);

            if (result.Success)
            {
                Status = ConnectionStatus.Connected;
                StatusText = "Connected";
                DisplayName = result.DisplayName ?? result.UserName;
                ServerVersion = result.AppVersion;
            }
            else
            {
                Status = ConnectionStatus.Error;
                StatusText = "Connection failed";
                ErrorMessage = result.ErrorMessage ?? "Unknown error";
            }
        }
        catch (Exception ex)
        {
            Status = ConnectionStatus.Error;
            StatusText = "Connection failed";
            ErrorMessage = ex.Message;
        }
    }

    private void ResetConnectionStatus()
    {
        Status = ConnectionStatus.Disconnected;
        StatusText = "Not connected";
        ErrorMessage = null;
        DisplayName = null;
        ServerVersion = null;
    }

    partial void OnStatusChanged(ConnectionStatus value)
    {
        StatusChanged?.Invoke(this, value);
    }
}
