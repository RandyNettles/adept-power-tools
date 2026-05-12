using System.CommandLine;
using System.CommandLine.Invocation;
using AdeptTools.Core.Api;
using AdeptTools.Core.Auth;
using AdeptTools.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AdeptTools.Cli.Commands;

public static class AuthCommands
{
    public static Command CreateAuthCommand()
    {
        var authCommand = new Command("auth", "Authentication commands");
        authCommand.AddCommand(CreateTestCommand());
        return authCommand;
    }

    private static Command CreateTestCommand()
    {
        var testCommand = new Command("test", "Test connection and authentication to an Adept server");

        testCommand.SetHandler(async (InvocationContext context) =>
        {
            var serviceProvider = context.BindingContext.GetRequiredService<IServiceProvider>();
            var settings = serviceProvider.GetRequiredService<AdeptToolSettings>();
            var authService = serviceProvider.GetRequiredService<IAdeptAuthService>();
            var apiClient = serviceProvider.GetRequiredService<IAdeptApiClient>();

            var serverDisplay = settings.MockMode
                ? "mock://localhost (mock mode)"
                : settings.ServerUrl ?? "unknown";

            Console.WriteLine();
            Console.WriteLine($"  Server:     {serverDisplay}");

            // Determine credentials
            string userName;
            string password;

            if (settings.MockMode)
            {
                userName = "MockUser";
                password = "mock";
            }
            else
            {
                userName = settings.UserName ?? "ADM";

                if (string.IsNullOrEmpty(settings.UserName))
                {
                    Console.Write("  User:       ");
                    userName = Console.ReadLine() ?? "ADM";
                }

                password = CredentialManager.PromptForPassword("  Password:   ");
            }

            // Login
            var authResult = await authService.LoginAsync(
                settings.ServerUrl ?? "mock://localhost",
                userName,
                password);

            if (!authResult.Success)
            {
                Console.WriteLine($"  Status:     \u001b[31m✗ Connection failed\u001b[0m");
                Console.WriteLine($"  Error:      {authResult.ErrorMessage}");
                Console.WriteLine();
                context.ExitCode = 1;
                return;
            }

            // Get additional info
            var userInfo = await apiClient.GetUserInfoAsync();

            Console.WriteLine($"  Version:    {userInfo.AppVersion ?? authResult.AppVersion ?? "unknown"}");
            Console.WriteLine($"  User:       {authResult.UserName} ({authResult.DisplayName})");

            if (settings.MockMode)
                Console.WriteLine($"  Status:     \u001b[32m✓ Connected (mock)\u001b[0m");
            else
                Console.WriteLine($"  Status:     \u001b[32m✓ Connected\u001b[0m");

            Console.WriteLine();
            context.ExitCode = 0;
        });

        return testCommand;
    }
}
