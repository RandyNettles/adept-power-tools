using System.Windows;
using AdeptTools.Backend.Http.Auth;
using AdeptTools.Backend.Http.Api;
using AdeptTools.Core.Auth;
using AdeptTools.Launcher.Services;
using AdeptTools.Launcher.ViewModels;
using AdeptTools.Workflow.Api;
using AdeptTools.Workflow.Input;
using AdeptTools.Workflow.Services;
using AdeptTools.Workflow.Validation;
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

        // Workflow services — returns mock or HTTP based on runtime toggle
        services.AddSingleton<MockWorkflowApiClient>();
        services.AddHttpClient<HttpWorkflowApiClient>();
        services.AddSingleton<Func<IWorkflowApiClient>>(sp =>
        {
            return () => sp.GetRequiredService<MockModeState>().IsMock
                ? sp.GetRequiredService<MockWorkflowApiClient>()
                : sp.GetRequiredService<HttpWorkflowApiClient>();
        });
        services.AddTransient<WorkflowExcelReader>();
        services.AddTransient<WorkflowXmlReader>();
        services.AddTransient<WorkflowValidator>();
        services.AddSingleton<Func<IWorkflowService>>(sp =>
        {
            return () =>
            {
                var apiClient = sp.GetRequiredService<Func<IWorkflowApiClient>>()();
                return new WorkflowService(
                    apiClient,
                    sp.GetRequiredService<WorkflowExcelReader>(),
                    sp.GetRequiredService<WorkflowXmlReader>(),
                    sp.GetRequiredService<WorkflowValidator>());
            };
        });

        // Navigation
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ConnectViewModel>();
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
