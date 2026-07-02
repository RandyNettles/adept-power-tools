using System.Windows;
using System.Net.Http;
using AdeptTools.Backend.Com.Api;
using AdeptTools.Backend.Com.Auth;
using AdeptTools.Backend.Com.Infrastructure;
using AdeptTools.Backend.Http.Auth;
using AdeptTools.Backend.Http.Api;
using AdeptTools.Backend.Http.Handlers;
using AdeptTools.Core.Auth;
using AdeptTools.Core.Configuration;
using AdeptTools.Core.Models;
using AdeptTools.Import.Api;
using AdeptTools.Import.Readers;
using AdeptTools.Import.Services;
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

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"Unhandled exception:\n\n{args.Exception.GetType().Name}: {args.Exception.Message}\n\n{args.Exception.StackTrace}",
                "Adept Tools — Crash Report",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                MessageBox.Show(
                    $"Fatal exception:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                    "Adept Tools — Fatal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            MessageBox.Show(
                $"Unobserved task exception:\n\n{args.Exception.InnerException?.Message ?? args.Exception.Message}\n\n{args.Exception.InnerException?.StackTrace ?? args.Exception.StackTrace}",
                "Adept Tools — Task Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.SetObserved();
        };

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
        var httpClientConfig = new HttpClientConfig();
        services.AddSingleton(httpClientConfig);
        services.AddSingleton(new AdeptToolSettings
        {
            Backend = BackendType.Com,
            MockMode = mockModeState.IsMock,
            ServerUrl = null,
            UserName = null,
            Verbose = false,
            LogPath = null
        });
        services.AddSingleton<ServerHistoryService>();
        services.AddSingleton<AuthSessionStore>();
        services.AddSingleton<ComProfileService>();

        // COM backend services
        services.AddSingleton<LegacyComFeatureFlags>();
        services.AddSingleton<ComOperationRunner>();
        services.AddSingleton<ComSessionManager>();
        services.AddSingleton<ILegacyCoreApiSession, LegacyCoreApiSession>();
        services.AddSingleton<ComAdeptAuthService>();
        services.AddSingleton<ComWorkflowApiClient>();
        services.AddSingleton<ComImportApiClient>();

        // Auth service factory — returns mock, HTTP, or COM based on runtime state
        services.AddSingleton<MockAdeptAuthService>();
        services.AddHttpClient("AdeptAuth");
        services.AddSingleton<HttpAdeptAuthService>(sp =>
        {
            var authHttpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("AdeptAuth");
            return new HttpAdeptAuthService(authHttpClient);
        });
        services.AddSingleton<IAdeptAuthService>(sp => sp.GetRequiredService<HttpAdeptAuthService>());
        services.AddSingleton<Func<BackendType, IAdeptAuthService>>(sp =>
        {
            return (backend) =>
            {
                if (sp.GetRequiredService<MockModeState>().IsMock)
                    return sp.GetRequiredService<MockAdeptAuthService>();
                return backend == BackendType.Com
                    ? sp.GetRequiredService<ComAdeptAuthService>()
                    : sp.GetRequiredService<HttpAdeptAuthService>();
            };
        });

        // Workflow services — returns mock or HTTP based on runtime toggle
        services.AddSingleton<MockWorkflowApiClient>();
        services.AddTransient<BearerTokenHandler>(sp =>
            new BearerTokenHandler(() =>
                sp.GetRequiredService<HttpAdeptAuthService>().AccessToken ?? httpClientConfig.AccessToken));
        services.AddHttpClient<HttpWorkflowApiClient>(client =>
        {
            if (!string.IsNullOrEmpty(httpClientConfig.BaseUrl))
                client.BaseAddress = new Uri(httpClientConfig.BaseUrl);
        }).AddHttpMessageHandler<BearerTokenHandler>();
        services.AddSingleton<Func<IWorkflowApiClient>>(sp =>
        {
            return () => sp.GetRequiredService<MockModeState>().IsMock
                ? sp.GetRequiredService<MockWorkflowApiClient>()
                : sp.GetRequiredService<ConnectViewModel>().SelectedBackend == BackendType.Com
                    ? sp.GetRequiredService<ComWorkflowApiClient>()
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

        // Import services — returns mock or HTTP based on runtime toggle
        services.AddSingleton<MockImportApiClient>();
        services.AddHttpClient<HttpImportApiClient>(client =>
        {
            if (!string.IsNullOrEmpty(httpClientConfig.BaseUrl))
                client.BaseAddress = new Uri(httpClientConfig.BaseUrl);
        }).AddHttpMessageHandler<BearerTokenHandler>();
        services.AddSingleton<Func<IImportApiClient>>(sp =>
        {
            return () => sp.GetRequiredService<MockModeState>().IsMock
                ? sp.GetRequiredService<MockImportApiClient>()
                : sp.GetRequiredService<ConnectViewModel>().SelectedBackend == BackendType.Com
                    ? sp.GetRequiredService<ComImportApiClient>()
                    : sp.GetRequiredService<HttpImportApiClient>();
        });
        services.AddTransient<ImportExcelReader>();
        services.AddTransient<ImportXmlConfigReader>();
        services.AddTransient<FieldResolver>();
        services.AddTransient<MappingValidator>();
        services.AddTransient<SearchBuilder>();
        services.AddTransient<AutoMapper>();
        services.AddSingleton<Func<IImportService>>(sp =>
        {
            return () =>
            {
                var apiClient = sp.GetRequiredService<Func<IImportApiClient>>()();
                return new ImportService(
                    apiClient,
                    sp.GetRequiredService<ImportExcelReader>(),
                    sp.GetRequiredService<ImportXmlConfigReader>(),
                    sp.GetRequiredService<FieldResolver>(),
                    sp.GetRequiredService<MappingValidator>(),
                    sp.GetRequiredService<SearchBuilder>(),
                    sp.GetRequiredService<AutoMapper>());
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
