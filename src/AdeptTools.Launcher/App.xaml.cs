using System.Windows;
using AdeptTools.Backend.Http.Auth;
using AdeptTools.Core.Auth;
using AdeptTools.Launcher.Services;
using AdeptTools.Launcher.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AdeptTools.Launcher;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Shared state
        var mockModeState = new MockModeState();
        services.AddSingleton(mockModeState);

        // Auth service factory — returns mock or HTTP based on runtime toggle
        services.AddSingleton<MockAdeptAuthService>();
        services.AddHttpClient<HttpAdeptAuthService>();
        services.AddSingleton<Func<IAdeptAuthService>>(sp =>
        {
            return () => sp.GetRequiredService<MockModeState>().IsMock
                ? sp.GetRequiredService<MockAdeptAuthService>()
                : sp.GetRequiredService<HttpAdeptAuthService>();
        });

        // Navigation
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<ConnectViewModel>();
        services.AddTransient<TemplateViewModel>();
        services.AddTransient<WorkflowViewModel>();
        services.AddTransient<ImportViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
