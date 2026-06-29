using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AdeptTools.Core.Auth;
using AdeptTools.Core.Models;
using AdeptTools.Launcher.Controls;
using AdeptTools.Launcher.Converters;
using AdeptTools.Launcher.Models;
using AdeptTools.Launcher.Services;

namespace AdeptTools.Launcher.ViewModels;

public partial class ConnectViewModel : ObservableObject
{
    private readonly Func<BackendType, IAdeptAuthService> _authServiceFactory;
    private readonly MockModeState _mockModeState;
    private readonly HttpClientConfig _httpClientConfig;
    private readonly ServerHistoryService _serverHistory;
    private readonly ComProfileService _comProfileService;
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
    [NotifyPropertyChangedFor(nameof(ShowComFields))]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
    private bool _isMockMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowComCredentials))]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditComProfileCommand))]
    private ComConnectionProfile? _selectedComProfile;

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
    public bool ShowComFields => SelectedBackend == BackendType.Com && !IsMockMode;
    public bool ShowComCredentials => ShowComFields && SelectedComProfile is not null;

    public ObservableCollection<string> ServerUrlHistory { get; } = new();
    public ObservableCollection<ComConnectionProfile> ComProfiles { get; } = new();

    public ConnectViewModel(
        Func<BackendType, IAdeptAuthService> authServiceFactory,
        MockModeState mockModeState,
        HttpClientConfig httpClientConfig,
        ServerHistoryService serverHistory,
        ComProfileService comProfileService)
    {
        _authServiceFactory = authServiceFactory;
        _mockModeState = mockModeState;
        _httpClientConfig = httpClientConfig;
        _serverHistory = serverHistory;
        _comProfileService = comProfileService;
        _isMockMode = _mockModeState.IsMock;

        _serverHistory.Load();
        foreach (var url in _serverHistory.Entries)
            ServerUrlHistory.Add(url);

        if (ServerUrlHistory.Count > 0)
            _serverUrl = ServerUrlHistory[0];

        if (!string.IsNullOrWhiteSpace(_serverHistory.LastUserName))
            _userName = _serverHistory.LastUserName;

        _comProfileService.Load();
        RefreshComProfiles();

        _mockModeState.Changed += (_, isMock) =>
        {
            IsMockMode = isMock;
        };
    }

    private void RefreshComProfiles()
    {
        var current = SelectedComProfile;
        ComProfiles.Clear();
        foreach (var p in _comProfileService.Profiles)
            ComProfiles.Add(p);
        SelectedComProfile = ComProfiles.FirstOrDefault(p => p.Name == current?.Name) ?? ComProfiles.FirstOrDefault();
    }

    partial void OnSelectedBackendChanged(BackendType value)
    {
        OnPropertyChanged(nameof(ShowHttpFields));
        OnPropertyChanged(nameof(ShowComFields));
        OnPropertyChanged(nameof(ShowComCredentials));
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
        if (SelectedBackend == BackendType.Com)
            return SelectedComProfile is not null && !string.IsNullOrWhiteSpace(UserName);
        return !string.IsNullOrWhiteSpace(ServerUrl) && !string.IsNullOrWhiteSpace(UserName);
    }

    [RelayCommand(CanExecute = nameof(CanTestConnection))]
    private async Task TestConnectionAsync(CancellationToken ct)
    {
        Status = ConnectionStatus.Connecting;
        StatusText = "Connecting...";
        ErrorMessage = null;
        DisplayName = null;
        ServerVersion = null;

        try
        {
            var authService = _authServiceFactory(SelectedBackend);
            var url = IsMockMode ? "mock://localhost"
                : SelectedBackend == BackendType.Com ? SelectedComProfile!.Address
                : ServerUrl;
            var user = IsMockMode ? "mock-user" : UserName;

            var result = await authService.LoginAsync(url, user, _password, ct);

            // Server has multiple Adept accounts linked to this identity — ask the user to pick one.
            if (result.RequiresUserSelection)
            {
                if (result.UserChoices is null || result.UserChoices.Count == 0)
                {
                    Status = ConnectionStatus.Error;
                    StatusText = "Connection failed";
                    ErrorMessage = "Multiple Adept accounts are linked to your identity but the account list could not be read from the server response. Please contact your administrator.";
                    return;
                }

                var dialogVm = new UserSelectionDialogViewModel(result.UserChoices);
                var dialog = new Controls.UserSelectionDialog
                {
                    DataContext = dialogVm,
                    Owner = System.Windows.Application.Current.MainWindow
                };
                dialog.ShowDialog();

                if (!dialogVm.Confirmed || dialogVm.SelectedUser is null)
                {
                    Status = ConnectionStatus.Disconnected;
                    StatusText = "Not connected";
                    return;
                }

                result = await authService.SelectUserAsync(dialogVm.SelectedUser.Id, dialogVm.SelectedUser.UserName, ct);
            }

            if (result.Success)
            {
                Status = ConnectionStatus.Connected;
                StatusText = "Connected";
                DisplayName = result.DisplayName ?? result.UserName;
                ServerVersion = result.AppVersion;

                // Store connection info for API clients
                _httpClientConfig.BaseUrl = url.TrimEnd('/') + "/";
                _httpClientConfig.AccessToken = result.AccessToken;

                // Remember successful server URL
                if (!IsMockMode)
                {
                    _serverHistory.Add(url, user);
                    ServerUrlHistory.Clear();
                    foreach (var entry in _serverHistory.Entries)
                        ServerUrlHistory.Add(entry);
                }
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

    [RelayCommand]
    private void AddComProfile()
    {
        var vm = new ComProfileDialogViewModel();
        var dialog = new ComProfileDialog { DataContext = vm, Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
        if (vm.DialogResult == true)
        {
            _comProfileService.Add(vm.ToProfile());
            RefreshComProfiles();
            SelectedComProfile = ComProfiles.LastOrDefault();
        }
    }

    private bool CanEditComProfile() => SelectedComProfile is not null;

    [RelayCommand(CanExecute = nameof(CanEditComProfile))]
    private void EditComProfile()
    {
        if (SelectedComProfile is null) return;
        var vm = new ComProfileDialogViewModel(SelectedComProfile);
        var dialog = new ComProfileDialog { DataContext = vm, Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
        if (vm.DialogResult == true)
        {
            _comProfileService.Update(SelectedComProfile, vm.ToProfile());
            var name = vm.ToProfile().Name;
            RefreshComProfiles();
            SelectedComProfile = ComProfiles.FirstOrDefault(p => p.Name == name) ?? ComProfiles.FirstOrDefault();
        }
    }
}
