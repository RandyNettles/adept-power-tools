using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AdeptTools.Launcher.Models;

namespace AdeptTools.Launcher.ViewModels;

public partial class ComProfileDialogViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Address))]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    private string _serverName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Address))]
    [NotifyPropertyChangedFor(nameof(IsTcpIp))]
    [NotifyPropertyChangedFor(nameof(IsHttp))]
    [NotifyPropertyChangedFor(nameof(IsHttps))]
    private ComConnectionProtocol _protocol = ComConnectionProtocol.Https;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Address))]
    private int _port = 443;

    public bool IsTcpIp
    {
        get => Protocol == ComConnectionProtocol.TcpIp;
        set { if (value) Protocol = ComConnectionProtocol.TcpIp; }
    }

    public bool IsHttp
    {
        get => Protocol == ComConnectionProtocol.Http;
        set { if (value) Protocol = ComConnectionProtocol.Http; }
    }

    public bool IsHttps
    {
        get => Protocol == ComConnectionProtocol.Https;
        set { if (value) Protocol = ComConnectionProtocol.Https; }
    }

    public string Address => Protocol switch
    {
        ComConnectionProtocol.TcpIp => $"{ServerName}:{Port}",
        ComConnectionProtocol.Http  => $"http://{ServerName}:{Port}/",
        ComConnectionProtocol.Https => $"https://{ServerName}:{Port}/",
        _ => string.Empty
    };

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(ServerName);

    private bool? _dialogResult;
    public bool? DialogResult
    {
        get => _dialogResult;
        private set => SetProperty(ref _dialogResult, value);
    }

    public string WindowTitle { get; }

    public ComProfileDialogViewModel(ComConnectionProfile? existing = null)
    {
        if (existing is not null)
        {
            _name = existing.Name;
            _serverName = existing.ServerName;
            _protocol = existing.Protocol;
            _port = existing.Port;
            WindowTitle = "Edit Data Source Profile";
        }
        else
        {
            WindowTitle = "New Data Source Profile";
        }
    }

    public ComConnectionProfile ToProfile() => new()
    {
        Name = Name.Trim(),
        ServerName = ServerName.Trim(),
        Protocol = Protocol,
        Port = Port
    };

    private bool CanOk() => IsValid;

    [RelayCommand(CanExecute = nameof(CanOk))]
    private void Ok()
    {
        DialogResult = true;
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
    }
}
