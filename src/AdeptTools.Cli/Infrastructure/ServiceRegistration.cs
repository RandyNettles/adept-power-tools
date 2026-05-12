using AdeptTools.Backend.Http.Api;
using AdeptTools.Backend.Http.Auth;
using AdeptTools.Core.Api;
using AdeptTools.Core.Auth;
using AdeptTools.Core.Models;
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
        }
        else if (backend == BackendType.Http)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
                throw new InvalidOperationException("--server is required when not using --mock mode.");

            var baseUri = new Uri(serverUrl);

            services.AddHttpClient<IAdeptAuthService, HttpAdeptAuthService>(client =>
            {
                client.BaseAddress = baseUri;
            });

            services.AddHttpClient<IAdeptApiClient, HttpAdeptApiClient>(client =>
            {
                client.BaseAddress = baseUri;
            });
        }
        else if (backend == BackendType.Com)
        {
            // COM backend implementations will be registered by Backend.Com (Plan 7)
            throw new InvalidOperationException("COM backend is not yet implemented. Use --backend http or --mock.");
        }
    }
}
