using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AdeptTools.Launcher.Converters;
using AdeptTools.Launcher.Services;

namespace AdeptTools.Launcher.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly MockModeState _mockModeState;

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private bool _isMockMode;

    [ObservableProperty]
    private string _appVersion;

    public ObservableCollection<NavItem> NavItems { get; }

    public MainViewModel(
        INavigationService navigationService,
        MockModeState mockModeState,
        ConnectViewModel connectViewModel)
    {
        _navigationService = navigationService;
        _mockModeState = mockModeState;
        _isMockMode = _mockModeState.IsMock;
        _appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        NavItems = new ObservableCollection<NavItem>
        {
            new("Connect", "\uE774", typeof(ConnectViewModel)),
            new("Templates", "\uE8A5", typeof(TemplateViewModel)),
            new("Workflows", "\uE912", typeof(WorkflowViewModel), isEnabled: false),
            new("Import", "\uE896", typeof(ImportViewModel), isEnabled: false)
        };

        _navigationService.CurrentPageChanged += vm =>
        {
            CurrentPage = vm;
            foreach (var item in NavItems)
                item.IsActive = item.ViewModelType == vm.GetType();
        };

        // Enable/disable feature pages based on connection status
        connectViewModel.StatusChanged += (_, status) =>
        {
            var isConnected = status == ConnectionStatus.Connected;
            foreach (var item in NavItems)
            {
                if (item.ViewModelType == typeof(WorkflowViewModel) ||
                    item.ViewModelType == typeof(ImportViewModel))
                {
                    item.IsEnabled = isConnected;
                }
            }
        };
    }

    public void NavigateToDefault()
    {
        Navigate(NavItems[0]);
    }

    [RelayCommand]
    private void Navigate(NavItem item)
    {
        if (!item.IsEnabled) return;
        _navigationService.NavigateTo(item.ViewModelType);
    }

    partial void OnIsMockModeChanged(bool value)
    {
        _mockModeState.IsMock = value;
    }
}
