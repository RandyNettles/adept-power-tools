using AdeptTools.Backend.Com.Api;
using AdeptTools.Backend.Com.Auth;
using AdeptTools.Backend.Com.Infrastructure;
using AdeptTools.Backend.Http.Api;
using AdeptTools.Backend.Http.Auth;
using AdeptTools.Backend.Http.Handlers;
using AdeptTools.Core.Api;
using AdeptTools.Core.Auth;
using AdeptTools.Core.Models;
using AdeptTools.Import.Api;
using AdeptTools.Import.Readers;
using AdeptTools.Import.Services;
using AdeptTools.Workflow.Api;
using AdeptTools.Workflow.Input;
using AdeptTools.Workflow.Services;
using AdeptTools.Workflow.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace AdeptTools.Cli.Infrastructure;

public static class ServiceRegistration
{
    public static void ConfigureServices(
        IServiceCollection services,
        BackendType backend,
        bool isMock,
        string? serverUrl)
    {
        if (isMock)
        {
            services.AddSingleton<IAdeptAuthService, MockAdeptAuthService>();
            services.AddSingleton<IAdeptApiClient, MockAdeptApiClient>();
            services.AddSingleton<IWorkflowApiClient, MockWorkflowApiClient>();
            services.AddSingleton<IImportApiClient, MockImportApiClient>();
        }
        else if (backend == BackendType.Http)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
                throw new InvalidOperationException("--server is required when not using --mock mode.");

            var normalizedServerUrl = serverUrl.TrimEnd('/') + "/";
            var baseUri = new Uri(normalizedServerUrl, UriKind.Absolute);

            services.AddHttpClient("AdeptAuth", client =>
            {
                client.BaseAddress = baseUri;
            });

            services.AddSingleton<IAdeptAuthService>(sp =>
            {
                var authHttpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("AdeptAuth");
                return new HttpAdeptAuthService(authHttpClient);
            });

            services.AddTransient<BearerTokenHandler>(sp =>
                new BearerTokenHandler(() => sp.GetRequiredService<IAdeptAuthService>().AccessToken));

            services.AddHttpClient<IAdeptApiClient, HttpAdeptApiClient>(client =>
            {
                client.BaseAddress = baseUri;
            }).AddHttpMessageHandler<BearerTokenHandler>();

            services.AddHttpClient<IWorkflowApiClient, HttpWorkflowApiClient>(client =>
            {
                client.BaseAddress = baseUri;
            }).AddHttpMessageHandler<BearerTokenHandler>();

            services.AddHttpClient<IImportApiClient, HttpImportApiClient>(client =>
            {
                client.BaseAddress = baseUri;
            }).AddHttpMessageHandler<BearerTokenHandler>();
        }
        else if (backend == BackendType.Com)
        {
            services.AddSingleton<LegacyComFeatureFlags>();
            services.AddSingleton<ComOperationRunner>();
            services.AddSingleton<ComSessionManager>();
            services.AddSingleton<ILegacyCoreApiSession, LegacyCoreApiSession>();
            services.AddSingleton<IAdeptAuthService, ComAdeptAuthService>();
            services.AddSingleton<IAdeptApiClient, ComAdeptApiClient>();
            services.AddSingleton<IWorkflowApiClient, ComWorkflowApiClient>();
            services.AddSingleton<IImportApiClient, ComImportApiClient>();
        }

        // Workflow services (backend-agnostic)
        services.AddTransient<IWorkflowService, WorkflowService>();
        services.AddTransient<WorkflowExcelReader>();
        services.AddTransient<WorkflowXmlReader>();
        services.AddTransient<WorkflowValidator>();

        // Import services (backend-agnostic)
        services.AddTransient<IImportService, ImportService>();
        services.AddTransient<ImportExcelReader>();
        services.AddTransient<ImportXmlConfigReader>();
        services.AddTransient<FieldResolver>();
        services.AddTransient<MappingValidator>();
        services.AddTransient<SearchBuilder>();
        services.AddTransient<AutoMapper>();
    }
}
